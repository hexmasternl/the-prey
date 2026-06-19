import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { GamesService } from './games.service';
import { environment } from '../../environments/environment';

describe('GamesService.checkAppVersion', () => {
  let service: GamesService;
  let httpMock: HttpTestingController;
  const url = `${environment.apiUrl}/games/version-checker`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GamesService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(GamesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the version under the current-version key', async () => {
    const promise = service.checkAppVersion('1.2.3');
    const req = httpMock.expectOne(url);

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ 'current-version': '1.2.3' });

    req.flush(null, { status: 204, statusText: 'No Content' });
    await expectAsync(promise).toBeResolvedTo('ok');
  });

  it('resolves to update-required on 409', async () => {
    const promise = service.checkAppVersion('1.0.0');
    httpMock
      .expectOne(url)
      .flush(null, { status: 409, statusText: 'Conflict' });

    await expectAsync(promise).toBeResolvedTo('update-required');
  });

  it('fails open to ok on 404 (older backend)', async () => {
    const promise = service.checkAppVersion('1.0.0');
    httpMock
      .expectOne(url)
      .flush(null, { status: 404, statusText: 'Not Found' });

    await expectAsync(promise).toBeResolvedTo('ok');
  });

  it('fails open to ok on a network error', async () => {
    const promise = service.checkAppVersion('1.0.0');
    httpMock.expectOne(url).error(new ProgressEvent('network error'));

    await expectAsync(promise).toBeResolvedTo('ok');
  });
});
