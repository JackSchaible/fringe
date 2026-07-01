import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faGripVertical, faXmark, faCircleNotch, faCircleInfo, faArrowUpRightFromSquare } from '@fortawesome/pro-solid-svg-icons';
import { faListOl } from '@fortawesome/pro-regular-svg-icons';
import { faMasksTheater, faClock, faLocationDot } from '@fortawesome/pro-light-svg-icons';
import { ApiService } from '../../services/api.service';
import { Show, Vote } from '../../models';

@Component({
  selector: 'fg-shows',
  imports: [FormsModule, CdkDropList, CdkDrag, FaIconComponent],
  templateUrl: './shows.html',
  styleUrl: './shows.scss',
})
export class ShowsPage implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly searchQuery = signal('');

  private readonly allShows = signal<Show[]>([]);
  readonly rankedShows = signal<Show[]>([]);
  readonly noShows = computed(() => this.allShows().length === 0);

  readonly filteredUnranked = computed(() => {
    const ranked = new Set(this.rankedShows().map(s => s.showId));
    const q = this.searchQuery().toLowerCase();
    return this.allShows()
      .filter(s => !ranked.has(s.showId))
      .filter(s =>
        !q ||
        s.title.toLowerCase().includes(q) ||
        (s.tag ?? '').toLowerCase().includes(q) ||
        (s.venue?.name ?? '').toLowerCase().includes(q)
      );
  });

  readonly selectedShow = signal<Show | null>(null);

  protected readonly faGripVertical = faGripVertical;
  protected readonly faXmark = faXmark;
  protected readonly faCircleNotch = faCircleNotch;
  protected readonly faCircleInfo = faCircleInfo;
  protected readonly faArrowUpRightFromSquare = faArrowUpRightFromSquare;
  protected readonly faListOl = faListOl;
  protected readonly faMasksTheater = faMasksTheater;
  protected readonly faClock = faClock;
  protected readonly faLocationDot = faLocationDot;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const [shows, votes] = await Promise.all([
      this.api.getShows().toPromise(),
      this.api.getVotes().toPromise(),
    ]);

    this.allShows.set(shows ?? []);

    if (votes?.length) {
      const voteMap = new Map<number, number>(votes.map(v => [v.showId, v.rank]));
      const ranked = (shows ?? [])
        .filter(s => voteMap.has(s.showId))
        .sort((a, b) => voteMap.get(a.showId)! - voteMap.get(b.showId)!);
      this.rankedShows.set(ranked);
    }

    this.loading.set(false);
  }

  onDrop(event: CdkDragDrop<Show[]>) {
    const arr = [...this.rankedShows()];
    moveItemInArray(arr, event.previousIndex, event.currentIndex);
    this.rankedShows.set(arr);
    this.saveVotes();
  }

  selectShow(show: Show, event: Event): void {
    event.stopPropagation();
    this.selectedShow.set(show);
  }

  fringeUrl(showId: number): string {
    return `https://tickets.fringetheatre.ca/event/601:${showId}`;
  }

  addToRanked(show: Show) {
    this.rankedShows.update(ranked => [...ranked, show]);
    this.saveVotes();
  }

  removeFromRanked(show: Show, event: Event) {
    event.stopPropagation();
    this.rankedShows.update(ranked => ranked.filter(s => s.showId !== show.showId));
    this.saveVotes();
  }

  private saveVotes() {
    this.saving.set(true);
    const votes: Vote[] = this.rankedShows().map((show, i) => ({
      showId: show.showId,
      rank: i + 1,
    }));
    this.api.saveVotes(votes).subscribe({ complete: () => this.saving.set(false) });
  }
}
