import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { PillGroupComponent, type PillOption } from './pill-group';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const options: ReadonlyArray<PillOption> = [
    { label: 'Comedy', value: 'Comedy' },
    { label: 'Drama', value: 'Drama' },
  ],
  build = async (
    selected: ReadonlySet<string> = new Set(),
  ): Promise<ComponentFixture<PillGroupComponent>> => {
    TestBed.configureTestingModule({
      imports: [PillGroupComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(PillGroupComponent);
    fixture.componentRef.setInput('options', options);
    fixture.componentRef.setInput('selected', selected);
    fixture.detectChanges();
    return fixture;
  };

describe('PillGroupComponent rendering', () => {
  it('renders a pill for each option', async () => {
    const fixture = await build(),
      pills = getNativeElement(fixture).querySelectorAll('.pill');
    expect(pills.length).toBe(options.length);
  });

  it('renders nothing when there are no options', async () => {
    TestBed.configureTestingModule({
      imports: [PillGroupComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(PillGroupComponent);
    fixture.componentRef.setInput('options', []);
    fixture.componentRef.setInput('selected', new Set());
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.filter-group'),
    ).toBeNull();
  });

  it('marks a selected option as active', async () => {
    const fixture = await build(new Set(['Comedy'])),
      [firstPill] =
        getNativeElement(fixture).querySelectorAll<HTMLButtonElement>(
          '.pill',
        );
    expect(firstPill.className).toContain('active');
  });
});

describe('PillGroupComponent events', () => {
  it('emits toggled with the option value when a pill is clicked', async () => {
    const fixture = await build(),
      emitted: Array<string> = [];
    fixture.componentInstance.toggled.subscribe((value) => {
      emitted.push(value);
    });
    const [firstPill] =
      getNativeElement(fixture).querySelectorAll<HTMLButtonElement>('.pill');
    firstPill.click();
    expect(emitted).toEqual(['Comedy']);
  });
});
