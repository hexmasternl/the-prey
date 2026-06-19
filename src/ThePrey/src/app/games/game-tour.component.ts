import {
  Component,
  computed,
  input,
  output,
  signal,
  OnDestroy,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** One coachmark step: the element to highlight and the translated copy to show. */
export interface TourStep {
  /** The element to spotlight, or null if it is not currently in the DOM (the step is skipped). */
  target: HTMLElement | null;
  titleKey: string;
  bodyKey: string;
}

/**
 * A lightweight, dependency-free guided tour ("coachmark") rendered over the live game view.
 * Spotlights each step's target element with a padded ring and a tooltip card, advancing on
 * Next and finishing on Done or Skip. Modeled on hunter-delay-overlay (in-page overlay + the
 * phosphor-green tactical tokens). Steps whose target is missing are skipped automatically.
 */
@Component({
  selector: 'app-game-tour',
  template: `
    @if (activeStep(); as step) {
      <div class="tour" (click)="skip()">
        <!-- Spotlight ring around the target; the huge box-shadow dims everything else. -->
        <div
          class="tour-spot"
          [style.top.px]="rect()!.top - 8"
          [style.left.px]="rect()!.left - 8"
          [style.width.px]="rect()!.width + 16"
          [style.height.px]="rect()!.height + 16"></div>

        <!-- Tooltip card. Stops propagation so taps on it don't dismiss the tour. -->
        <div
          class="tour-card"
          [class.tour-card--above]="placeAbove()"
          [style.top.px]="cardTop()"
          (click)="$event.stopPropagation()">
          <div class="tour-card-title">{{ step.titleKey | translate }}</div>
          <div class="tour-card-body">{{ step.bodyKey | translate }}</div>
          <div class="tour-card-actions">
            <button class="tour-skip" (click)="skip()">{{ 'GAME_TOUR.SKIP' | translate }}</button>
            <button class="tour-next" (click)="next()">
              {{ (isLastStep() ? 'GAME_TOUR.DONE' : 'GAME_TOUR.NEXT') | translate }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .tour {
        position: fixed;
        inset: 0;
        z-index: 2000;
      }
      .tour-spot {
        position: fixed;
        border: 1px solid var(--tp-signal);
        border-radius: 4px;
        box-shadow: 0 0 0 9999px rgba(var(--tp-bg-void-rgb), 0.78);
        pointer-events: none;
        transition: all 120ms ease;
      }
      .tour-card {
        position: fixed;
        left: 50%;
        transform: translateX(-50%);
        width: min(320px, calc(100vw - 32px));
        background: rgba(var(--tp-bg-void-rgb), 0.96);
        border: 1px solid rgba(var(--tp-signal-rgb), 0.5);
        border-radius: 3px;
        padding: 16px 18px;
      }
      .tour-card-title {
        font-family: var(--tp-head);
        font-size: 13px;
        letter-spacing: 2px;
        text-transform: uppercase;
        color: var(--tp-signal);
      }
      .tour-card-body {
        font-family: var(--tp-body);
        font-size: 13px;
        line-height: 1.6;
        color: var(--tp-text-soft);
        margin-top: 6px;
      }
      .tour-card-actions {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-top: 14px;
      }
      .tour-skip {
        background: none;
        border: none;
        font-family: var(--tp-body);
        font-size: 12px;
        letter-spacing: 1px;
        text-transform: uppercase;
        color: var(--tp-text-soft);
      }
      .tour-next {
        background: rgba(var(--tp-signal-rgb), 0.12);
        border: 1px solid var(--tp-signal);
        border-radius: 3px;
        padding: 8px 18px;
        font-family: var(--tp-head);
        font-size: 12px;
        letter-spacing: 2px;
        text-transform: uppercase;
        color: var(--tp-signal);
      }

      @media (prefers-color-scheme: light) {
        .tour-card-title,
        .tour-next {
          color: var(--tp-signal-dim);
        }
        .tour-spot {
          border-color: var(--tp-signal-dim);
        }
      }
    `,
  ],
  imports: [TranslatePipe],
})
export class GameTourComponent implements OnDestroy {
  /** Ordered steps. Steps whose target is null are skipped. */
  readonly steps = input.required<TourStep[]>();

  /** Emitted when the tour finishes — last step passed, skipped, or no resolvable steps. */
  readonly completed = output<void>();

  /** Index into steps(); advanced past any step whose target is missing. */
  private readonly index = signal(0);

  /** Bumped on resize/orientation so the spotlight rect recomputes. */
  private readonly geometryTick = signal(0);

  private readonly onReflow = () => this.geometryTick.update((v) => v + 1);

  constructor() {
    window.addEventListener('resize', this.onReflow);
    window.addEventListener('orientationchange', this.onReflow);
  }

  /** The first step at or after the current index that has a resolvable target. */
  readonly activeStep = computed<TourStep | null>(() => {
    const steps = this.steps();
    for (let i = this.index(); i < steps.length; i++) {
      if (steps[i].target) return steps[i];
    }
    return null;
  });

  /** Bounding rect of the active step's target (recomputed on reflow). */
  readonly rect = computed(() => {
    this.geometryTick();
    return this.activeStep()?.target?.getBoundingClientRect() ?? null;
  });

  /** True when there is no further resolvable step after the active one. */
  readonly isLastStep = computed(() => {
    const steps = this.steps();
    const active = this.activeStep();
    if (!active) return true;
    const activeIndex = steps.indexOf(active);
    for (let i = activeIndex + 1; i < steps.length; i++) {
      if (steps[i].target) return false;
    }
    return true;
  });

  /** Place the card above the target when the target sits in the lower part of the viewport. */
  readonly placeAbove = computed(() => {
    const r = this.rect();
    return r ? r.top > window.innerHeight * 0.55 : false;
  });

  /** Top offset (px) for the card: below the target, or above it for low targets. */
  readonly cardTop = computed(() => {
    const r = this.rect();
    if (!r) return 0;
    return this.placeAbove() ? Math.max(16, r.top - 150) : r.bottom + 16;
  });

  next(): void {
    if (this.isLastStep()) {
      this.finish();
      return;
    }
    // Advance past the active step; activeStep() then resolves to the next visible one.
    const steps = this.steps();
    const active = this.activeStep();
    const from = active ? steps.indexOf(active) + 1 : steps.length;
    this.index.set(from);
    // If nothing resolvable remains, close.
    if (!this.activeStep()) this.finish();
  }

  skip(): void {
    this.finish();
  }

  private finish(): void {
    this.completed.emit();
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.onReflow);
    window.removeEventListener('orientationchange', this.onReflow);
  }
}
