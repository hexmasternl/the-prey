import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { ToastController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';
import { Observable, firstValueFrom, throwError } from 'rxjs';
import { httpErrorInterceptor } from './http-error.interceptor';
import { environment } from '../../environments/environment';

describe('httpErrorInterceptor', () => {
  let toastCtrl: jasmine.SpyObj<ToastController>;
  let toast: jasmine.SpyObj<HTMLIonToastElement>;
  let translate: jasmine.SpyObj<TranslateService>;
  let injector: EnvironmentInjector;

  beforeEach(() => {
    toast = jasmine.createSpyObj<HTMLIonToastElement>('toast', ['present']);
    toast.present.and.resolveTo();
    toastCtrl = jasmine.createSpyObj<ToastController>('ToastController', ['create']);
    toastCtrl.create.and.resolveTo(toast);
    translate = jasmine.createSpyObj<TranslateService>('TranslateService', ['instant']);
    translate.instant.and.callFake((key: string | string[]) => key);

    TestBed.configureTestingModule({
      providers: [
        { provide: ToastController, useValue: toastCtrl },
        { provide: TranslateService, useValue: translate },
      ],
    });
    injector = TestBed.inject(EnvironmentInjector);
  });

  function run(status: number): Observable<HttpEvent<unknown>> {
    const req = new HttpRequest('GET', `${environment.apiUrl}/games`);
    const next: HttpHandlerFn = () => throwError(() => new HttpErrorResponse({ status }));
    return runInInjectionContext(injector, () => httpErrorInterceptor(req, next));
  }

  it('toasts on server/network errors, stays silent on 4xx, and always rethrows', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2030, 0, 1));

    // 5xx → toast
    await expectAsync(firstValueFrom(run(500))).toBeRejected();
    expect(toastCtrl.create).toHaveBeenCalledTimes(1);

    // advance past the duplicate-suppression window
    jasmine.clock().tick(5000);

    // network loss (status 0) → toast
    await expectAsync(firstValueFrom(run(0))).toBeRejected();
    expect(toastCtrl.create).toHaveBeenCalledTimes(2);

    jasmine.clock().tick(5000);

    // routine 4xx → no toast (left to the calling page)
    await expectAsync(firstValueFrom(run(404))).toBeRejected();
    expect(toastCtrl.create).toHaveBeenCalledTimes(2);

    jasmine.clock().uninstall();
  });

  it('stays silent on non-HTTP errors (e.g. a failed Auth0 token refresh) and rethrows', async () => {
    const req = new HttpRequest('GET', `${environment.apiUrl}/games`);
    const authError = new Error('Login required');
    const next: HttpHandlerFn = () => throwError(() => authError);

    await expectAsync(
      firstValueFrom(runInInjectionContext(injector, () => httpErrorInterceptor(req, next))),
    ).toBeRejectedWith(authError);

    expect(toastCtrl.create).not.toHaveBeenCalled();
  });

  it('ignores non-API origins', async () => {
    const req = new HttpRequest('GET', 'https://tiles.example.com/1/2/3.png');
    const next: HttpHandlerFn = () => throwError(() => new HttpErrorResponse({ status: 500 }));

    await expectAsync(
      firstValueFrom(runInInjectionContext(injector, () => httpErrorInterceptor(req, next))),
    ).toBeRejected();

    expect(toastCtrl.create).not.toHaveBeenCalled();
  });
});
