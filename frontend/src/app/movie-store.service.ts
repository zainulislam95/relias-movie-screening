import {
  computed,
  DestroyRef,
  inject,
  Injectable,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Subscription } from "rxjs";
import { errorMessage, MovieApiService } from "./movie-api.service";
import { Genre, Movie, MoviePage, MovieSearch } from "./movie";

export const INITIAL_SEARCH: MovieSearch = {
  title: "",
  year: null,
  genre: "",
  sortBy: "rating",
  direction: "desc",
};

@Injectable({ providedIn: "root" })
export class MovieStore {
  private readonly api = inject(MovieApiService);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private pages: MoviePage[] = [];
  private query: MovieSearch = { ...INITIAL_SEARCH };
  private lastRequest: { cursor?: string; index: number } = { index: 0 };

  readonly genres = signal<Genre[]>([]);
  readonly genreError = signal("");
  readonly genresLoading = signal(false);
  readonly movies = signal<Movie[]>([]);
  readonly notice = signal<string | null>(null);
  readonly loading = signal(false);
  readonly searchError = signal("");
  readonly pageIndex = signal(0);
  readonly hasMore = signal(false);
  readonly favorites = signal<Movie[]>([]);
  readonly favoritesLoading = signal(false);
  readonly favoritesReady = signal(false);
  readonly favoritesError = signal("");
  readonly actionError = signal("");
  readonly announcement = signal("");
  readonly pending = signal(new Set<string>());
  readonly favoriteIds = computed(
    () => new Set(this.favorites().map((movie) => movie.id)),
  );

  constructor() {
    this.destroy.onDestroy(() => this.request?.unsubscribe());
  }

  initialize() {
    this.loadGenres();
    this.loadFavorites();
    this.search(INITIAL_SEARCH);
  }

  loadGenres() {
    if (this.genresLoading()) return;
    this.genresLoading.set(true);
    this.genreError.set("");
    this.api
      .genres()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (genres) => {
          this.genres.set(genres);
          this.genresLoading.set(false);
        },
        error: () => {
          this.genreError.set(
            "Genre filters are unavailable. You can still search by title or year.",
          );
          this.genresLoading.set(false);
        },
      });
  }

  loadFavorites() {
    if (this.favoritesLoading() || this.pending().size) return;
    this.favoritesLoading.set(true);
    this.favoritesError.set("");
    this.api
      .favorites()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (movies) => {
          this.favorites.set(movies);
          this.favoritesReady.set(true);
          this.favoritesLoading.set(false);
        },
        error: () => {
          this.favoritesError.set(
            "Your favorites could not be loaded. Please retry.",
          );
          this.favoritesReady.set(false);
          this.favoritesLoading.set(false);
        },
      });
  }

  search(query: MovieSearch) {
    this.query = { ...query };
    this.pages = [];
    this.pageIndex.set(0);
    this.fetch(0);
  }

  retry() {
    this.fetch(this.lastRequest.index, this.lastRequest.cursor);
  }

  next() {
    if (this.loading() || !this.hasMore()) return;
    const next = this.pageIndex() + 1;
    if (this.pages[next]) this.display(next);
    else {
      const cursor = this.pages[this.pageIndex()]?.nextCursor;
      if (cursor) this.fetch(next, cursor);
    }
  }

  previous() {
    if (!this.loading() && this.pageIndex() > 0)
      this.display(this.pageIndex() - 1);
  }

  private fetch(index: number, cursor?: string) {
    this.request?.unsubscribe();
    this.lastRequest = { index, cursor };
    this.loading.set(true);
    this.searchError.set("");
    this.request = this.api.search(this.query, cursor).subscribe({
      next: (page) => {
        this.pages[index] = page;
        this.display(index);
        this.loading.set(false);
      },
      error: (error) => {
        this.searchError.set(errorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private display(index: number) {
    const page = this.pages[index];
    this.pageIndex.set(index);
    this.movies.set(page.items);
    this.hasMore.set(page.hasMore && !!page.nextCursor);
    this.notice.set(page.notice);
    this.searchError.set("");
  }

  toggleFavorite(movie: Movie) {
    if (
      !this.favoritesReady() ||
      this.favoritesLoading() ||
      this.pending().has(movie.id)
    )
      return;
    const removing = this.favoriteIds().has(movie.id);
    this.pending.update((ids) => new Set([...ids, movie.id]));
    this.actionError.set("");
    const action = removing
      ? this.api.removeFavorite(movie.id)
      : this.api.addFavorite(movie.id);
    action.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => {
        this.favorites.update((movies) =>
          removing
            ? movies.filter((item) => item.id !== movie.id)
            : [movie, ...movies],
        );
        this.announcement.set(
          `${movie.title} ${removing ? "removed from" : "added to"} favorites.`,
        );
        this.finishAction(movie.id);
      },
      error: (error) => {
        this.actionError.set(errorMessage(error));
        this.finishAction(movie.id);
      },
    });
  }

  private finishAction(id: string) {
    this.pending.update((ids) => {
      const next = new Set(ids);
      next.delete(id);
      return next;
    });
  }
}
