import { Component, computed, input, output, signal } from '@angular/core';
import { PillGroupComponent, type PillOption } from '../pill-group/pill-group';
import { faChevronDown, faChevronUp } from '@fortawesome/pro-light-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { RangeSliderComponent } from '../range-slider/range-slider';
import type { Show } from '../../../models';

const EMPTY_COUNT = 0,
  ONE_COUNT = 1;

const byLocaleCompare = (first: string, second: string): number =>
  first.localeCompare(second);

@Component({
  imports: [PillGroupComponent, RangeSliderComponent, FaIconComponent],
  selector: 'fg-shows-filters',
  styleUrl: './shows-filters.scss',
  templateUrl: './shows-filters.html',
})
export class ShowsFiltersComponent {
  public readonly shows = input.required<ReadonlyArray<Show>>();
  public readonly selectedGenres = input.required<ReadonlySet<string>>();
  public readonly selectedRatings = input.required<ReadonlySet<string>>();
  public readonly selectedLocations = input.required<ReadonlySet<string>>();
  public readonly priceRange = input.required<readonly [number, number]>();
  public readonly priceBounds = input.required<readonly [number, number]>();
  public readonly durationRange = input.required<readonly [number, number]>();
  public readonly durationBounds = input.required<readonly [number, number]>();

  public readonly toggleGenre = output<string>();
  public readonly toggleRating = output<string>();
  public readonly toggleLocation = output<string>();
  public readonly priceRangeChange = output<readonly [number, number]>();
  public readonly durationRangeChange = output<readonly [number, number]>();
  public readonly clear = output();

  protected readonly expanded = signal(false);

  protected readonly faChevronDown = faChevronDown;
  protected readonly faChevronUp = faChevronUp;

  protected readonly genreOptions = computed<ReadonlyArray<PillOption>>(() => {
    const tags = this.shows()
      .map((show) => show.tag)
      .filter((tag): tag is string => Boolean(tag));
    return [...new Set(tags)]
      .sort(byLocaleCompare)
      .map((tag) => ({ label: tag, value: tag }));
  });

  protected readonly ratingOptions = computed<ReadonlyArray<PillOption>>(
    () => {
      const byCode = new Map<string, PillOption>();
      for (const show of this.shows()) {
        if (show.contentRating) {
          byCode.set(show.contentRating.code, {
            label: show.contentRating.code,
            title: show.contentRating.name,
            value: show.contentRating.code,
          });
        }
      }
      return [...byCode.values()].sort((first, second) =>
        byLocaleCompare(first.value, second.value),
      );
    },
  );

  protected readonly locationOptions = computed<ReadonlyArray<PillOption>>(
    () => {
      const names = this.shows()
        .map((show) => show.venue?.name)
        .filter((name): name is string => Boolean(name));
      return [...new Set(names)]
        .sort(byLocaleCompare)
        .map((name) => ({ label: name, value: name }));
    },
  );

  protected readonly hasActiveFilters = computed<boolean>(
    () =>
      this.selectedGenres().size > EMPTY_COUNT ||
      this.selectedRatings().size > EMPTY_COUNT ||
      this.selectedLocations().size > EMPTY_COUNT ||
      this.priceRange()[EMPTY_COUNT] !== this.priceBounds()[EMPTY_COUNT] ||
      this.priceRange()[ONE_COUNT] !== this.priceBounds()[ONE_COUNT] ||
      this.durationRange()[EMPTY_COUNT] !==
        this.durationBounds()[EMPTY_COUNT] ||
      this.durationRange()[ONE_COUNT] !== this.durationBounds()[ONE_COUNT],
  );

  protected toggleExpanded(): void {
    this.expanded.update((current) => !current);
  }
}
