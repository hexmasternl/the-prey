import {
  Component,
  computed,
  input,
  OnDestroy,
  signal,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Countdown overlay shown centered over the map until the hunter is allowed to move
 * (`hunterMayMoveAt` from the game status). Ticks locally every second; the absolute
 * timestamp input means every status poll resyncs it for free. Renders nothing when
 * the moment is null or already in the past, and removes itself when it reaches zero.
 */
@Component({
  selector: 'app-hunter-delay-overlay',
  template: `
    @if (secondsLeft() > 0) {
      <div class="delay-overlay">
        <div class="delay-card">
          <div class="delay-label">{{ 'HUNTER_DELAY.LABEL' | translate }}</div>
          <div class="delay-countdown">{{ countdown() }}</div>
          <div class="delay-hint">{{ 'HUNTER_DELAY.HINT' | translate }}</div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .delay-overlay {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        pointer-events: none;
      }
      .delay-card {
        background: rgba(10, 14, 10, 0.85);
        border: 1px solid rgba(100, 255, 0, 0.5);
        border-radius: 8px;
        padding: 20px 32px;
        text-align: center;
      }
      .delay-label {
        font-size: 12px;
        letter-spacing: 2px;
        text-transform: uppercase;
        color: #64ff00;
      }
      .delay-countdown {
        font-size: 48px;
        font-weight: 700;
        font-variant-numeric: tabular-nums;
        color: #ffffff;
        line-height: 1.2;
      }
      .delay-hint {
        font-size: 12px;
        color: rgba(255, 255, 255, 0.7);
      }
    `,
  ],
  imports: [TranslatePipe],
})
export class HunterDelayOverlayComponent implements OnDestroy {
  /** ISO timestamp at which the hunter may move; null hides the overlay. */
  readonly hunterMayMoveAt = input<string | null>(null);

  private readonly now = signal(Date.now());
  private readonly timer = setInterval(() => this.now.set(Date.now()), 1000);

  readonly secondsLeft = computed(() => {
    const at = this.hunterMayMoveAt();
    if (!at) return 0;
    return Math.max(0, Math.ceil((new Date(at).getTime() - this.now()) / 1000));
  });

  readonly countdown = computed(() => {
    const s = this.secondsLeft();
    const mins = Math.floor(s / 60)
      .toString()
      .padStart(2, '0');
    const secs = (s % 60).toString().padStart(2, '0');
    return `${mins}:${secs}`;
  });

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }
}
