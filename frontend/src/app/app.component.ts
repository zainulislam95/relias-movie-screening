import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import { MovieCardComponent } from "./movie-card.component";
import { INITIAL_SEARCH, MovieStore } from "./movie-store.service";
import { MovieSearch } from "./movie";

@Component({
  selector: "app-root",
  imports: [ReactiveFormsModule, MovieCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./app.component.html",
})
export class AppComponent {
  readonly store = inject(MovieStore);
  readonly view = signal<"discover" | "favorites">("discover");
  readonly form = new FormGroup({
    title: new FormControl("", {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    year: new FormControl<number | null>(null, [
      Validators.min(1888),
      Validators.max(2100),
      Validators.pattern(/^\d+$/),
    ]),
    genre: new FormControl("", { nonNullable: true }),
    sort: new FormControl("rating:desc", { nonNullable: true }),
  });

  constructor() {
    this.store.initialize();
  }

  search() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const values = this.form.getRawValue();
    const [sortBy, direction] = values.sort.split(":") as [
      MovieSearch["sortBy"],
      MovieSearch["direction"],
    ];
    this.store.search({
      title: values.title,
      year: values.year,
      genre: values.genre,
      sortBy,
      direction,
    });
  }

  reset() {
    this.form.reset({ title: "", year: null, genre: "", sort: "rating:desc" });
    this.store.search(INITIAL_SEARCH);
  }
}
