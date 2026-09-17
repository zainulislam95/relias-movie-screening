import { expect, Page, test } from "@playwright/test";
import type { Movie, MoviePage } from "../src/app/movie";

function poster(title: string, color: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="600"><defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="${color}"/><stop offset="1" stop-color="#122538"/></linearGradient></defs><rect width="400" height="600" fill="url(#g)"/><circle cx="260" cy="160" r="90" fill="#edc598" opacity=".7"/><path d="M0 420 130 190 260 420 340 270 400 420V600H0" fill="#122538" opacity=".85"/><text x="30" y="490" fill="#fff3d9" font-size="33" font-family="Georgia">${title}</text><text x="32" y="523" fill="#cbd0ce" font-size="11" letter-spacing="4">A STORY WORTH WATCHING</text></svg>`;
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}

const movies: Movie[] = [
  {
    id: "101",
    title: "Beyond the Horizon",
    originalTitle: "Beyond the Horizon",
    year: 2024,
    rating: 89,
    overview:
      "An unexpected journey brings two strangers together on the road to a distant coast.",
    posterUrl: poster("Beyond the Horizon", "#a56949"),
    genres: [{ id: "drama", name: "Drama" }],
  },
  {
    id: "102",
    title: "The Quiet Hours",
    originalTitle: "The Quiet Hours",
    year: 2023,
    rating: 84,
    overview:
      "One summer, a small town discovers that even ordinary days can change everything.",
    posterUrl: poster("The Quiet Hours", "#487b77"),
    genres: [{ id: "drama", name: "Drama" }],
  },
  {
    id: "103",
    title: "Between the Stars",
    originalTitle: "Between the Stars",
    year: 2022,
    rating: 91,
    overview:
      "A crew on the edge of the known universe receives a message from home.",
    posterUrl: poster("Between the Stars", "#54668f"),
    genres: [{ id: "scifi", name: "Science Fiction" }],
  },
  {
    id: "104",
    title: "The Last Light",
    originalTitle: "The Last Light",
    year: 2020,
    rating: 87,
    overview:
      "A photographer follows a trail of letters through a city full of forgotten stories.",
    posterUrl: poster("The Last Light", "#85595f"),
    genres: [{ id: "drama", name: "Drama" }],
  },
];
const result: MoviePage = {
  items: movies,
  hasMore: true,
  nextCursor: "opaque?x=1&y=2+",
  notice: null,
};

async function mockApi(page: Page) {
  let favorites: Movie[] = [];
  let failAction = false;
  await page.route("**/api/genres", (route) =>
    route.fulfill({
      json: [
        { id: "drama", name: "Drama" },
        { id: "scifi", name: "Science Fiction" },
      ],
    }),
  );
  await page.route("**/api/movies?*", (route) =>
    route.fulfill({ json: result }),
  );
  await page.route("**/api/favorites", (route) =>
    route.fulfill({ json: favorites }),
  );
  await page.route("**/api/favorites/*", (route) => {
    if (failAction)
      return route.fulfill({ status: 503, json: { title: "Unavailable" } });
    const id = route.request().url().split("/").pop();
    if (route.request().method() === "PUT") {
      favorites = [
        ...favorites.filter((movie) => movie.id !== id),
        movies.find((movie) => movie.id === id)!,
      ];
      return route.fulfill({ status: 201 });
    }
    favorites = favorites.filter((movie) => movie.id !== id);
    return route.fulfill({ status: 204 });
  });
  return {
    failAction: (value: boolean) => {
      failAction = value;
    },
  };
}

test("combines filters, encodes cursors, caches previous pages, and resets paging on sort", async ({
  page,
}) => {
  await mockApi(page);
  const requests: URL[] = [];
  await page.route("**/api/movies?*", (route) => {
    requests.push(new URL(route.request().url()));
    return route.fulfill({ json: result });
  });
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: movies[0].title, exact: true }),
  ).toBeVisible();
  await page.getByLabel("Movie title", { exact: true }).fill("A & B");
  await page.getByLabel("Release year").fill("2024");
  await page.getByLabel("Genre", { exact: true }).selectOption("drama");
  await page.getByRole("button", { name: "Search movies" }).click();
  await expect
    .poll(() => requests.at(-1)?.searchParams.get("title"))
    .toBe("A & B");
  expect(requests.at(-1)!.searchParams.get("year")).toBe("2024");
  expect(requests.at(-1)!.searchParams.get("genre")).toBe("drama");
  await page.getByRole("button", { name: "Next →", exact: true }).click();
  await expect(page.getByText("Page 2", { exact: true })).toBeVisible();
  expect(requests.at(-1)!.searchParams.get("cursor")).toBe(result.nextCursor);
  const count = requests.length;
  await page.getByRole("button", { name: "← Previous" }).click();
  await expect(page.getByText("Page 1", { exact: true })).toBeVisible();
  expect(requests.length).toBe(count);
  await page.getByLabel("Sort by", { exact: true }).selectOption("year:asc");
  await expect
    .poll(() => requests.at(-1)?.searchParams.get("direction"))
    .toBe("asc");
  expect(requests.at(-1)!.searchParams.has("cursor")).toBe(false);
});

test("saves favorites, keeps them across reload, and removes them from both views", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await page
    .getByRole("button", { name: "Save Beyond the Horizon to favorites" })
    .click();
  await expect(page.getByRole("button", { name: "Favorites 1" })).toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "Favorites 1" }).click();
  await expect(
    page.getByRole("heading", { name: "Beyond the Horizon", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Remove Beyond the Horizon from favorites" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Your next movie night starts here." }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Discover", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Save Beyond the Horizon to favorites" }),
  ).toHaveAttribute("aria-pressed", "false");
});

test("failed favorite changes keep the existing state and allow retry", async ({
  page,
}) => {
  const api = await mockApi(page);
  api.failAction(true);
  await page.goto("/");
  await page
    .getByRole("button", { name: "Save Beyond the Horizon to favorites" })
    .click();
  await expect(page.getByRole("alert")).toContainText(
    "Your favorites have not changed",
  );
  await expect(page.getByRole("button", { name: "Favorites 0" })).toBeVisible();
  api.failAction(false);
  await page
    .getByRole("button", { name: "Save Beyond the Horizon to favorites" })
    .click();
  await expect(page.getByRole("button", { name: "Favorites 1" })).toBeVisible();
  api.failAction(true);
  await page
    .getByRole("button", { name: "Remove Beyond the Horizon from favorites" })
    .click();
  await expect(page.getByRole("alert")).toContainText(
    "Your favorites have not changed",
  );
  await expect(page.getByRole("button", { name: "Favorites 1" })).toBeVisible();
});

test("shows loading, error, retry, and empty results", async ({ page }) => {
  await mockApi(page);
  let fail = true;
  await page.route("**/api/movies?*", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 250));
    await route.fulfill(
      fail
        ? { status: 503, json: { title: "Unavailable" } }
        : { json: { ...result, items: [], hasMore: false, nextCursor: null } },
    );
  });
  await page.goto("/");
  await expect(page.getByText("Finding your next great watch…")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "A brief intermission" }),
  ).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(
    page.getByRole("heading", { name: "No matches this time." }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Next →", exact: true }),
  ).toBeDisabled();
});

test("does not let an older search overwrite newer results", async ({
  page,
}) => {
  await mockApi(page);
  let releaseSlow!: () => void;
  const waitForRelease = new Promise<void>((resolve) => {
    releaseSlow = resolve;
  });
  await page.route("**/api/movies?*", async (route) => {
    const title = new URL(route.request().url()).searchParams.get("title");
    if (title === "Slow") await waitForRelease;
    await route
      .fulfill({
        json: {
          ...result,
          items: [{ ...movies[0], title: title || movies[0].title }],
        },
      })
      .catch(() => {});
  });
  await page.goto("/");
  await page.getByLabel("Movie title", { exact: true }).fill("Slow");
  const slowRequest = page.waitForRequest((request) =>
    request.url().includes("title=Slow"),
  );
  await page.getByRole("button", { name: "Search movies" }).click();
  await slowRequest;
  await page.getByLabel("Movie title", { exact: true }).fill("Fast");
  await page.getByRole("button", { name: "Search movies" }).click();
  await expect(
    page.getByRole("heading", { name: "Fast", exact: true }),
  ).toBeVisible();
  releaseSlow();
  await expect(
    page.getByRole("heading", { name: "Slow", exact: true }),
  ).toHaveCount(0);
});

test("validates the year before making a search request", async ({ page }) => {
  await mockApi(page);
  let requests = 0;
  page.on("request", (request) => {
    if (request.url().includes("/api/movies?")) requests++;
  });
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: movies[0].title, exact: true }),
  ).toBeVisible();
  const before = requests;
  await page.getByLabel("Release year").fill("1700");
  await page.getByRole("button", { name: "Search movies" }).click();
  await expect(
    page.getByText("Enter a whole year between 1888 and 2100."),
  ).toBeVisible();
  expect(requests).toBe(before);
});

test("favorites outage disables save buttons until the list can be loaded", async ({
  page,
}) => {
  await mockApi(page);
  let fail = true;
  await page.route("**/api/favorites", (route) =>
    route.fulfill(fail ? { status: 500, json: {} } : { json: [] }),
  );
  await page.goto("/");
  await expect(
    page.getByRole("button", { name: "Save Beyond the Horizon to favorites" }),
  ).toBeDisabled();
  fail = false;
  await page.getByRole("button", { name: "Retry favorites" }).click();
  await expect(
    page.getByRole("button", { name: "Save Beyond the Horizon to favorites" }),
  ).toBeEnabled();
});

test("supports mobile layout and missing posters", async ({
  page,
}, testInfo) => {
  await mockApi(page);
  await page.route("**/api/movies?*", (route) =>
    route.fulfill({
      json: {
        ...result,
        items: [{ ...movies[0], posterUrl: null }, ...movies.slice(1)],
      },
    }),
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(page.getByText("Poster unavailable")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("mobile.png"),
    fullPage: true,
  });
});

test("renders the desktop discovery page", async ({ page }, testInfo) => {
  await mockApi(page);
  await page.setViewportSize({ width: 1440, height: 1080 });
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Beyond the Horizon", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("desktop.png"),
    fullPage: true,
  });
});
