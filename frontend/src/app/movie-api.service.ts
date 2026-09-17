import {
  HttpClient,
  HttpErrorResponse,
  HttpParams,
} from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { Genre, Movie, MoviePage, MovieSearch } from "./movie";

@Injectable({ providedIn: "root" })
export class MovieApiService {
  private readonly http = inject(HttpClient);

  search(search: MovieSearch, cursor?: string) {
    let params = new HttpParams()
      .set("sortBy", search.sortBy)
      .set("direction", search.direction);
    if (search.title.trim()) params = params.set("title", search.title.trim());
    if (search.year !== null) params = params.set("year", search.year);
    if (search.genre) params = params.set("genre", search.genre);
    if (cursor) params = params.set("cursor", cursor);
    return this.http.get<MoviePage>("/api/movies", { params });
  }
  genres() {
    return this.http.get<Genre[]>("/api/genres");
  }
  favorites() {
    return this.http.get<Movie[]>("/api/favorites");
  }
  addFavorite(id: string) {
    return this.http.put<void>(
      `/api/favorites/${encodeURIComponent(id)}`,
      null,
    );
  }
  removeFavorite(id: string) {
    return this.http.delete<void>(`/api/favorites/${encodeURIComponent(id)}`);
  }
}

export function errorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse))
    return "Something went wrong. Please try again.";
  if (error.status === 0 || error.status === 504)
    return "We could not reach the movie service. Please try again in a moment.";
  if (error.status === 503)
    return "Movie search is temporarily unavailable. Please try again later.";
  if (error.status === 404)
    return "This movie is no longer available. Please try another movie.";
  if (error.status === 400)
    return "Please check your search details and try again.";
  return "We could not complete that request. Please try again.";
}
