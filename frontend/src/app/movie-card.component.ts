import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
  signal,
} from "@angular/core";
import { Movie } from "./movie";

@Component({
  selector: "app-movie-card",
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="movie-card">
      <div class="poster">
        @if (movie().posterUrl && !imageFailed()) {
          <img
            [src]="movie().posterUrl"
            [alt]="movie().title + ' poster'"
            loading="lazy"
            (error)="imageFailed.set(true)"
          />
        } @else {
          <div class="poster-placeholder">
            <span aria-hidden="true">▶</span><span>{{ movie().title }}</span
            ><small>Poster unavailable</small>
          </div>
        }
        @if (movie().rating !== null) {
          <span
            class="rating"
            [attr.aria-label]="'Rating: ' + movie().rating + ' out of 100'"
            ><span aria-hidden="true">★</span> {{ movie().rating
            }}<small>/100</small></span
          >
        }
        <button
          class="save-button"
          [class.saved]="saved()"
          [disabled]="disabled() || busy()"
          [attr.aria-pressed]="saved()"
          [attr.aria-label]="
            (saved() ? 'Remove ' : 'Save ') +
            movie().title +
            (saved() ? ' from favorites' : ' to favorites')
          "
          [title]="saved() ? 'Remove from favorites' : 'Save to favorites'"
          (click)="favoriteToggle.emit()"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M6 4h12v17l-6-4-6 4z" />
          </svg>
          <span class="sr-only">{{ busy() ? "Saving change" : "" }}</span>
        </button>
      </div>
      <div class="movie-details">
        <div class="movie-meta">
          <span>{{ movie().year ?? "Year unavailable" }}</span
          ><span>{{ movie().genres[0]?.name ?? "Movie" }}</span>
        </div>
        <h3>{{ movie().title }}</h3>
        <p>
          {{ movie().overview || "No description available for this movie." }}
        </p>
      </div>
    </article>
  `,
})
export class MovieCardComponent {
  readonly movie = input.required<Movie>();
  readonly saved = input(false);
  readonly busy = input(false);
  readonly disabled = input(false);
  readonly favoriteToggle = output<void>();
  readonly imageFailed = signal(false);
}
