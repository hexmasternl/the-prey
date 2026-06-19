import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { GameTourComponent, TourStep } from './game-tour.component';

describe('GameTourComponent', () => {
  let fixture: ComponentFixture<GameTourComponent>;
  let component: GameTourComponent;

  function makeTarget(): HTMLElement {
    const el = document.createElement('div');
    document.body.appendChild(el);
    return el;
  }

  function setSteps(steps: TourStep[]): void {
    fixture.componentRef.setInput('steps', steps);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GameTourComponent],
      providers: [provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(GameTourComponent);
    component = fixture.componentInstance;
  });

  it('advances steps in order and completes on the last step', () => {
    const completed = jasmine.createSpy('completed');
    component.completed.subscribe(completed);

    setSteps([
      { target: makeTarget(), titleKey: 'A_T', bodyKey: 'A_B' },
      { target: makeTarget(), titleKey: 'B_T', bodyKey: 'B_B' },
    ]);

    expect(component.activeStep()?.titleKey).toBe('A_T');
    expect(component.isLastStep()).toBeFalse();

    component.next();
    expect(component.activeStep()?.titleKey).toBe('B_T');
    expect(component.isLastStep()).toBeTrue();
    expect(completed).not.toHaveBeenCalled();

    component.next();
    expect(completed).toHaveBeenCalledTimes(1);
  });

  it('completes immediately on skip', () => {
    const completed = jasmine.createSpy('completed');
    component.completed.subscribe(completed);

    setSteps([{ target: makeTarget(), titleKey: 'A_T', bodyKey: 'A_B' }]);
    component.skip();

    expect(completed).toHaveBeenCalledTimes(1);
  });

  it('skips a step whose target is missing', () => {
    setSteps([
      { target: null, titleKey: 'A_T', bodyKey: 'A_B' },
      { target: makeTarget(), titleKey: 'B_T', bodyKey: 'B_B' },
    ]);

    // First step has no target, so the resolvable active step is the second one.
    expect(component.activeStep()?.titleKey).toBe('B_T');
    expect(component.isLastStep()).toBeTrue();
  });

  it('renders nothing when no step has a resolvable target', () => {
    setSteps([{ target: null, titleKey: 'A_T', bodyKey: 'A_B' }]);

    expect(component.activeStep()).toBeNull();
    expect(fixture.nativeElement.querySelector('.tour')).toBeNull();
  });
});
