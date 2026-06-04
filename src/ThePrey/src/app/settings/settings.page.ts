import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, filter, switchMap, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { LanguageService, SupportedLanguage } from '../i18n/language.service';
import { SettingsService } from './settings.service';

@Component({
  selector: 'app-settings',
  templateUrl: 'settings.page.html',
  styleUrls: ['settings.page.scss'],
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    ReactiveFormsModule,
    TranslatePipe,
  ],
})
export class SettingsPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly settingsService = inject(SettingsService);
  private readonly languageService = inject(LanguageService);
  private readonly destroyRef = inject(DestroyRef);

  saveStatus: 'idle' | 'saving' | 'saved' | 'error' = 'idle';
  isLoading = true;
  callsignFocused = false;

  readonly form = this.fb.group({
    callsign: [
      '',
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(30),
        Validators.pattern(/^[a-zA-Z0-9 \-_&$#@]+$/),
      ],
    ],
    language: [this.languageService.current as string],
  });

  get callsign() {
    return this.form.controls.callsign;
  }

  get callsignError(): string {
    const ctrl = this.callsign;
    if (!ctrl.errors) return '';
    if (ctrl.errors['required']) return 'SETTINGS.CALLSIGN_ERROR_REQUIRED';
    if (ctrl.errors['minlength']) return 'SETTINGS.CALLSIGN_ERROR_MIN';
    if (ctrl.errors['pattern']) return 'SETTINGS.CALLSIGN_ERROR_PATTERN';
    return '';
  }

  get callsignLength(): number {
    return this.callsign.value?.length ?? 0;
  }

  ngOnInit(): void {
    this.settingsService.get().subscribe({
      next: (settings) => {
        this.form.patchValue(
          { callsign: settings.callsign, language: settings.language },
          { emitEvent: false },
        );
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });

    this.form.valueChanges
      .pipe(
        debounceTime(600),
        filter(() => this.form.valid && !this.isLoading),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        tap(() => { this.saveStatus = 'saving'; }),
        switchMap((value) =>
          this.settingsService
            .save({ callsign: value.callsign ?? '', language: value.language ?? 'en' })
            .pipe(
              catchError(() => {
                this.saveStatus = 'error';
                return of(null);
              }),
            ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        if (result !== null) {
          this.saveStatus = 'saved';
          setTimeout(() => {
            if (this.saveStatus === 'saved') this.saveStatus = 'idle';
          }, 2500);
        }
      });
  }

  selectLanguage(lang: SupportedLanguage): void {
    this.form.patchValue({ language: lang }, { emitEvent: true });
    this.languageService.setLanguage(lang);
  }

  back(): void {
    this.router.navigate(['/home']);
  }
}
