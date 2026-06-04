import { Injectable, inject } from '@angular/core';
import { Preferences } from '@capacitor/preferences';
import { TranslateService } from '@ngx-translate/core';

export const SUPPORTED_LANGUAGES = ['en', 'nl'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

const LANGUAGE_PREFERENCE_KEY = 'language';
const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

/**
 * Resolves and switches the app language. The active language is, in order of precedence:
 * the user's persisted choice, the device language (when supported), or English.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  /** Applies the persisted or device language. Called once at app startup. */
  async init(): Promise<void> {
    const { value: stored } = await Preferences.get({ key: LANGUAGE_PREFERENCE_KEY });
    const language = isSupported(stored) ? stored : deviceLanguage();
    this.translate.use(language);
  }

  get current(): SupportedLanguage {
    return isSupported(this.translate.getCurrentLang()) ? (this.translate.getCurrentLang() as SupportedLanguage) : DEFAULT_LANGUAGE;
  }

  /** Switches the active language and persists the choice. */
  async setLanguage(language: SupportedLanguage): Promise<void> {
    this.translate.use(language);
    await Preferences.set({ key: LANGUAGE_PREFERENCE_KEY, value: language });
  }
}

function deviceLanguage(): SupportedLanguage {
  const candidate = (navigator.language ?? '').slice(0, 2).toLowerCase();
  return isSupported(candidate) ? candidate : DEFAULT_LANGUAGE;
}

function isSupported(value: string | null | undefined): value is SupportedLanguage {
  return SUPPORTED_LANGUAGES.includes((value ?? '') as SupportedLanguage);
}
