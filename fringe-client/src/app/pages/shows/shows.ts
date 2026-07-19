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
import { faCircleNotch } from '@fortawesome/pro-solid-svg-icons';
import { faListOl } from '@fortawesome/pro-regular-svg-icons';
import { firstValueFrom } from 'rxjs';

const EMPTY_COUNT = 0,
  RANK_BASE = 1;

@Component({
  imports: [
    FormsModule,
    CdkDropList,
    FaIconComponent,
    RankedShowItemComponent,
    BrowseShowCardComponent,
    ShowDetailPanelComponent,
  ],
  selector: 'fg-shows',
  styleUrl: './shows.scss',
  templateUrl: './shows.html',
})
export class ShowsPage implements OnInit {
  public readonly loading = signal(true);
  public readonly saving = signal(false);
  public readonly searchQuery = signal('');

  public readonly rankedShows = signal<Array<Show>>([]);
  public readonly noShows = computed(
    () => this.allShows().length === EMPTY_COUNT,
  );

  public readonly filteredUnranked = computed(() => {
    const ranked = new Set(this.rankedShows().map((show) => show.showId)),
      query = this.searchQuery().toLowerCase();
    return this.allShows()
      .filter((show) => !ranked.has(show.showId))
      .filter(
        (show) =>
          !query ||
          show.title.toLowerCase().includes(query) ||
          (show.tag ?? '').toLowerCase().includes(query) ||
          (show.venue?.name ?? '').toLowerCase().includes(query),
      );
  });

  public readonly selectedShow = signal<Show | null>(null);

  protected readonly faCircleNotch = faCircleNotch;
  protected readonly faListOl = faListOl;

  private readonly allShows = signal<Array<Show>>([]);

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
