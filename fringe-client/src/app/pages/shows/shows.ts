import {
  type CdkDragDrop,
  CdkDropList,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import {
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import type { Show, Vote } from '../../models';
import { ApiService } from '../../services/api.service';
import { BrowseShowCardComponent } from './browse-show-card/browse-show-card';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';
import { RankedShowItemComponent } from './ranked-show-item/ranked-show-item';
import { ShowDetailPanelComponent } from './show-detail-panel/show-detail-panel';
import { ShowsFiltersComponent } from './shows-filters/shows-filters';
import { faCircleNotch } from '@fortawesome/pro-solid-svg-icons';
import { faListOl } from '@fortawesome/pro-regular-svg-icons';
import { firstValueFrom } from 'rxjs';

const EMPTY_COUNT = 0,
  RANK_BASE = 1;

const parsePrice = (show: Show): number =>
  Number.parseFloat(show.price) || EMPTY_COUNT;

const toggleSetEntry = (
  set: ReadonlySet<string>,
  value: string,
): ReadonlySet<string> => {
  const next = new Set(set);
  if (next.has(value)) {
    next.delete(value);
  } else {
    next.add(value);
  }
  return next;
};

const computeBounds = (
  values: ReadonlyArray<number>,
): readonly [number, number] => {
  if (values.length === EMPTY_COUNT) {
    return [EMPTY_COUNT, EMPTY_COUNT];
  }
  return [Math.min(...values), Math.max(...values)];
};

@Component({
  imports: [
    FormsModule,
    CdkDropList,
    FaIconComponent,
    RankedShowItemComponent,
    BrowseShowCardComponent,
    ShowDetailPanelComponent,
    ShowsFiltersComponent,
  ],
  selector: 'fg-shows',
  styleUrl: './shows.scss',
  templateUrl: './shows.html',
})
export class ShowsPage implements OnInit {
  public readonly loading = signal(true);
  public readonly saving = signal(false);
  public readonly searchQuery = signal('');

  public readonly allShows = signal<Array<Show>>([]);
  public readonly rankedShows = signal<Array<Show>>([]);
  public readonly noShows = computed(
    () => this.allShows().length === EMPTY_COUNT,
  );

  public readonly selectedGenres = signal<ReadonlySet<string>>(new Set());
  public readonly selectedRatings = signal<ReadonlySet<string>>(new Set());
  public readonly selectedLocations = signal<ReadonlySet<string>>(new Set());

  public readonly priceBounds = computed<readonly [number, number]>(() =>
    computeBounds(this.allShows().map(parsePrice)),
  );
  public readonly durationBounds = computed<readonly [number, number]>(() =>
    computeBounds(this.allShows().map((show) => show.lengthInMinutes)),
  );
  public readonly priceRange = computed<readonly [number, number]>(
    () => this.priceRangeOverride() ?? this.priceBounds(),
  );
  public readonly durationRange = computed<readonly [number, number]>(
    () => this.durationRangeOverride() ?? this.durationBounds(),
  );

  public readonly filteredUnranked = computed(() => {
    const ranked = new Set(this.rankedShows().map((show) => show.showId)),
      query = this.searchQuery().toLowerCase(),
      genres = this.selectedGenres(),
      ratings = this.selectedRatings(),
      locations = this.selectedLocations(),
      [minPrice, maxPrice] = this.priceRange(),
      [minDuration, maxDuration] = this.durationRange();

    return this.allShows()
      .filter((show) => !ranked.has(show.showId))
      .filter(
        (show) =>
          !query ||
          show.title.toLowerCase().includes(query) ||
          (show.tag ?? '').toLowerCase().includes(query) ||
          (show.venue?.name ?? '').toLowerCase().includes(query),
      )
      .filter(
        (show) => genres.size === EMPTY_COUNT || genres.has(show.tag ?? ''),
      )
      .filter(
        (show) =>
          ratings.size === EMPTY_COUNT ||
          ratings.has(show.contentRating?.code ?? ''),
      )
      .filter(
        (show) =>
          locations.size === EMPTY_COUNT ||
          locations.has(show.venue?.name ?? ''),
      )
      .filter((show) => {
        const price = parsePrice(show);
        return price >= minPrice && price <= maxPrice;
      })
      .filter(
        (show) =>
          show.lengthInMinutes >= minDuration &&
          show.lengthInMinutes <= maxDuration,
      );
  });

  public readonly selectedShow = signal<Show | null>(null);

  protected readonly faCircleNotch = faCircleNotch;
  protected readonly faListOl = faListOl;

  private readonly priceRangeOverride = signal<
    readonly [number, number] | null
  >(null);
  private readonly durationRangeOverride = signal<
    readonly [number, number] | null
  >(null);

  private readonly api = inject(ApiService);

  public ngOnInit(): void {
    void this.load();
  }

  public onDrop(event: CdkDragDrop<Array<Show>>): void {
    const reordered = [...this.rankedShows()];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.rankedShows.set(reordered);
    this.saveVotes();
  }

  public addToRanked(show: Show): void {
    this.rankedShows.update((ranked) => [...ranked, show]);
    this.saveVotes();
  }

  public removeFromRanked(show: Show): void {
    this.rankedShows.update((ranked) =>
      ranked.filter((entry) => entry.showId !== show.showId),
    );
    this.saveVotes();
  }

  public toggleGenre(genre: string): void {
    this.selectedGenres.update((current) => toggleSetEntry(current, genre));
  }

  public toggleRating(rating: string): void {
    this.selectedRatings.update((current) => toggleSetEntry(current, rating));
  }

  public toggleLocation(location: string): void {
    this.selectedLocations.update((current) =>
      toggleSetEntry(current, location),
    );
  }

  public setPriceRange(range: readonly [number, number]): void {
    this.priceRangeOverride.set(range);
  }

  public setDurationRange(range: readonly [number, number]): void {
    this.durationRangeOverride.set(range);
  }

  public clearFilters(): void {
    this.selectedGenres.set(new Set());
    this.selectedRatings.set(new Set());
    this.selectedLocations.set(new Set());
    this.priceRangeOverride.set(null);
    this.durationRangeOverride.set(null);
  }

  private async load(): Promise<void> {
    const [shows, votes] = await Promise.all([
      firstValueFrom(this.api.getShows()),
      firstValueFrom(this.api.getVotes()),
    ]);

    this.allShows.set(shows);

    if (votes.length > EMPTY_COUNT) {
      const voteMap = new Map<number, number>(
          votes.map((vote) => [vote.showId, vote.rank]),
        ),
        ranked = shows
          .filter((show) => voteMap.has(show.showId))
          .sort(
            (first, second) =>
              (voteMap.get(first.showId) ?? EMPTY_COUNT) -
              (voteMap.get(second.showId) ?? EMPTY_COUNT),
          );
      this.rankedShows.set(ranked);
    }

    this.loading.set(false);
  }

  private saveVotes(): void {
    this.saving.set(true);
    const votes: Array<Vote> = this.rankedShows().map((show, index) => ({
      rank: index + RANK_BASE,
      showId: show.showId,
    }));
    this.api.saveVotes(votes).subscribe({
      complete: () => {
        this.saving.set(false);
      },
    });
  }
}
