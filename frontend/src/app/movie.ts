export interface Genre {
  id: string;
  name: string;
}
export interface Movie {
  id: string;
  title: string;
  originalTitle: string;
  year: number | null;
  overview: string;
  rating: number | null;
  posterUrl: string | null;
  genres: Genre[];
}
export interface MoviePage {
  items: Movie[];
  hasMore: boolean;
  nextCursor: string | null;
  notice: string | null;
}
export interface MovieSearch {
  title: string;
  year: number | null;
  genre: string;
  sortBy: "rating" | "title" | "year";
  direction: "asc" | "desc";
}
