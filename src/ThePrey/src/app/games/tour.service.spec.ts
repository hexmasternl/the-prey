import { TourService } from './tour.service';

// NOTE: the Capacitor `Preferences` export cannot be spied on in this test environment, so these
// tests exercise the real web implementation (backed by window.localStorage), clearing it between
// tests and injecting faults at the Storage layer.
describe('TourService', () => {
  let service: TourService;

  beforeEach(() => {
    localStorage.clear();
    service = new TourService();
  });

  afterEach(() => localStorage.clear());

  it('returns false when a role has not been seen', async () => {
    expect(await service.hasSeen('prey')).toBeFalse();
    expect(await service.hasSeen('hunter')).toBeFalse();
  });

  it('returns true after the role is marked seen', async () => {
    await service.markSeen('prey');
    expect(await service.hasSeen('prey')).toBeTrue();
  });

  it('tracks hunter and prey independently', async () => {
    await service.markSeen('prey');

    expect(await service.hasSeen('prey')).toBeTrue();
    expect(await service.hasSeen('hunter')).toBeFalse();
  });

  it('treats a read failure as not seen', async () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('storage unavailable');

    expect(await service.hasSeen('hunter')).toBeFalse();
  });

  it('does not throw when a write fails', async () => {
    spyOn(Storage.prototype, 'setItem').and.throwError('storage unavailable');

    await expectAsync(service.markSeen('hunter')).toBeResolved();
  });
});
