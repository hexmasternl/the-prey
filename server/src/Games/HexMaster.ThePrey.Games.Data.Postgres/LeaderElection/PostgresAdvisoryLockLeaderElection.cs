using System.Data;
using HexMaster.ThePrey.Games.LeaderElection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HexMaster.ThePrey.Games.Data.Postgres.LeaderElection;

/// <summary>
/// Leader election backed by a PostgreSQL session-level advisory lock. The leader holds a dedicated,
/// long-lived connection — kept separate from EF Core's pooled connections, because advisory locks are
/// connection-scoped and EF returns its connections to the pool between operations (which would release
/// the lock). If the leader process dies, Postgres releases the session lock automatically when the
/// backend connection closes, so a standby acquires leadership on its next attempt.
/// </summary>
public sealed class PostgresAdvisoryLockLeaderElection : ILeaderElection, IAsyncDisposable
{
    // Stable, arbitrary application lock key shared by every replica ("TPREY" sweep lock).
    private const long LockKey = 0x_54_50_52_45_59_01;

    private readonly string _connectionString;
    private readonly ILogger<PostgresAdvisoryLockLeaderElection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private NpgsqlConnection? _lockConnection;
    private bool _isLeader;

    public PostgresAdvisoryLockLeaderElection(
        string connectionString,
        ILogger<PostgresAdvisoryLockLeaderElection> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Fast path: we already hold the lock — confirm the connection is still alive.
            if (_isLeader && _lockConnection is { State: ConnectionState.Open })
            {
                if (await IsConnectionAliveAsync(_lockConnection, ct))
                    return true;

                _logger.LogWarning("Leader lock connection was lost; will attempt to re-acquire leadership.");
                await ResetConnectionAsync();
            }

            return await AcquireAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Leader election attempt failed; treating this replica as a standby for this tick.");
            await ResetConnectionAsync();
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> AcquireAsync(CancellationToken ct)
    {
        await ResetConnectionAsync();

        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using (var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection))
        {
            cmd.Parameters.AddWithValue("key", LockKey);
            var acquired = await cmd.ExecuteScalarAsync(ct) is true;

            if (!acquired)
            {
                // Another replica is the leader — don't keep an idle connection open while standing by.
                await connection.DisposeAsync();
                _isLeader = false;
                return false;
            }
        }

        _lockConnection = connection;
        _isLeader = true;
        return true;
    }

    private static async Task<bool> IsConnectionAliveAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        try
        {
            await using var ping = new NpgsqlCommand("SELECT 1", connection);
            await ping.ExecuteScalarAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ResetConnectionAsync()
    {
        _isLeader = false;
        if (_lockConnection is null)
            return;

        try
        {
            await _lockConnection.DisposeAsync();
        }
        catch
        {
            // Best effort — the backend may already be gone.
        }
        finally
        {
            _lockConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_lockConnection is { State: ConnectionState.Open })
            {
                try
                {
                    await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _lockConnection);
                    cmd.Parameters.AddWithValue("key", LockKey);
                    await cmd.ExecuteScalarAsync();
                }
                catch
                {
                    // Disposing the connection releases the session lock regardless.
                }
            }

            await ResetConnectionAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
