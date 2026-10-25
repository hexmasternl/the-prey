package nl.hexmaster.theprey.ui.games.create

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.FakeProfileStore
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.game.CreateGameResult
import nl.hexmaster.theprey.data.game.NewGame
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelection
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class CreateGameViewModelTest {

    /** Each create waits until the test answers it, unless [answer] is set. */
    private class FakeGameRepository : UnusedGameRepository() {
        var answer: (() -> CreateGameResult)? = { CreateGameResult.Created("game-1") }
        val requests = mutableListOf<NewGame>()
        val pending = mutableListOf<CompletableDeferred<CreateGameResult>>()

        override suspend fun createGame(game: NewGame): CreateGameResult {
            requests += game
            answer?.let { return it() }
            return CompletableDeferred<CreateGameResult>().also { pending += it }.await()
        }
    }

    private val repository = FakeGameRepository()
    private val profileStore = FakeProfileStore(aProfile(callsign = "Fox"))
    private val vondelpark = PlayFieldSelection("pf-1", "Vondelpark", isPublic = true)

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun viewModel(handle: SavedStateHandle = SavedStateHandle()) =
        CreateGameViewModel(repository, profileStore, handle)

    private fun TestScope.collectEvents(sut: CreateGameViewModel): MutableList<CreateGameEvent> {
        val events = mutableListOf<CreateGameEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.events.collect { events += it } }
        return events
    }

    @Test
    fun canCreate_ShouldBeFalse_WhenNoPlayFieldIsChosen() = runTest {
        val sut = viewModel()

        assertFalse(sut.uiState.value.canCreate)
        sut.create()
        assertTrue(repository.requests.isEmpty())
    }

    @Test
    fun canCreate_ShouldBeTrue_WhenPlayFieldIsChosen() = runTest {
        val sut = viewModel()

        sut.onPlayFieldSelected(vondelpark)

        assertTrue(sut.uiState.value.canCreate)
        assertEquals(vondelpark, sut.uiState.value.playField)
    }

    @Test
    fun create_ShouldSendChosenValuesAndCallsign_WhenCreating() = runTest {
        val sut = viewModel()
        sut.onPlayFieldSelected(vondelpark)
        sut.onSettingChange(GameSetting.Duration, 60)

        sut.create()

        assertEquals(NewGame("pf-1", "Fox", 60, 5, 10, 3, 1), repository.requests.single())
    }

    @Test
    fun create_ShouldFallBackToDisplayName_WhenCallsignIsBlank() = runTest {
        profileStore.save(aProfile(callsign = " ", displayName = "Nina Hawk"))
        val sut = viewModel()
        sut.onPlayFieldSelected(vondelpark)

        sut.create()

        assertEquals("Nina Hawk", repository.requests.single().displayName)
    }

    @Test
    fun create_ShouldBlockSecondSubmitAndSettingChanges_WhenSubmitting() = runTest {
        repository.answer = null
        val sut = viewModel()
        sut.onPlayFieldSelected(vondelpark)

        sut.create()
        assertTrue(sut.uiState.value.submitting)
        assertFalse(sut.uiState.value.canCreate)
        sut.create()
        sut.onSettingChange(GameSetting.Duration, 90)
        sut.onPlayFieldSelected(PlayFieldSelection("pf-2", "Other", isPublic = false))

        assertEquals(1, repository.requests.size)
        assertEquals(30, sut.uiState.value.settings.duration)
        assertEquals(vondelpark, sut.uiState.value.playField)
    }

    @Test
    fun create_ShouldEmitCreatedOnce_WhenServerCreatesTheGame() = runTest {
        val sut = viewModel()
        val events = collectEvents(sut)
        sut.onPlayFieldSelected(vondelpark)

        sut.create()

        assertEquals(listOf<CreateGameEvent>(CreateGameEvent.Created("game-1")), events)
        assertFalse(sut.uiState.value.submitting)
    }

    @Test
    fun create_ShouldKeepChoicesAndRetrySameRequest_WhenRequestFails() = runTest {
        repository.answer = { CreateGameResult.Failed }
        val sut = viewModel()
        val events = collectEvents(sut)
        sut.onPlayFieldSelected(vondelpark)
        sut.onSettingChange(GameSetting.HunterDelay, 10)

        sut.create()

        assertTrue(sut.uiState.value.failed)
        assertFalse(sut.uiState.value.submitting)
        assertEquals(vondelpark, sut.uiState.value.playField)
        assertEquals(10, sut.uiState.value.settings.hunterDelay)
        assertTrue(events.isEmpty())

        repository.answer = { CreateGameResult.Created("game-2") }
        sut.create()

        assertEquals(2, repository.requests.size)
        assertEquals(repository.requests[0], repository.requests[1])
        assertFalse(sut.uiState.value.failed)
        assertEquals(listOf<CreateGameEvent>(CreateGameEvent.Created("game-2")), events)
    }

    @Test
    fun onSettingChange_ShouldClampFinalPing_WhenPingIntervalIsLowered() = runTest {
        val sut = viewModel()
        sut.onSettingChange(GameSetting.FinalPingInterval, 3)

        sut.onSettingChange(GameSetting.PingInterval, 2)

        assertEquals(2, sut.uiState.value.settings.finalPingInterval)
    }

    @Test
    fun init_ShouldRestorePlayFieldSettingsAndPickerFlag_WhenSavedStateHoldsThem() = runTest {
        val handle = SavedStateHandle()
        viewModel(handle).apply {
            onPickerLaunched()
            onPlayFieldSelected(vondelpark)
            onSettingChange(GameSetting.Duration, 90)
            onSettingChange(GameSetting.PingInterval, 2)
            onSettingChange(GameSetting.FinalPingInterval, 2)
        }

        val restored = viewModel(handle)

        assertEquals(vondelpark, restored.uiState.value.playField)
        assertEquals(GameSettings(duration = 90, pingInterval = 2, finalPingInterval = 2), restored.uiState.value.settings)
        assertTrue(restored.pickerLaunched)
        assertFalse(restored.firstPickCancelled)
    }

    @Test
    fun firstPickCancelled_ShouldBeTrue_WhenPickerLaunchedWithoutSelection() = runTest {
        val sut = viewModel()
        assertFalse(sut.pickerLaunched)
        assertFalse(sut.firstPickCancelled)

        sut.onPickerLaunched()

        assertTrue(sut.firstPickCancelled)
    }
}
