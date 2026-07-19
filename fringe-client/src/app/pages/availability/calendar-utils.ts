import type { Show } from '../../models';

const DAYS_PER_WEEK = 7,
  EMPTY_COUNT = 0,
  FIRST_DAY_OF_MONTH = 1,
  HOURS_PER_DAY = 24,
  JS_MONTH_OFFSET = 1,
  LAST_DAY_SENTINEL = 0,
  LOOP_START = 0,
  MINUTES_PER_HALF_HOUR = 30,
  MINUTES_PER_HOUR = 60,
  MS_PER_MINUTE = 60_000,
  ON_THE_HOUR_MINUTES = 0,
  PAD_WIDTH = 2,
  STEP = 1;

export interface FestivalDay {
  readonly date: string;
  readonly earliest: string;
  readonly latest: string;
  readonly hasWindows: boolean;
}

export interface CalendarCell {
  readonly date: string | null;
  readonly dayNumber: number | null;
  readonly isFestival: boolean;
  readonly festivalDay: FestivalDay | null;
}

export interface CalendarMonth {
  readonly label: string;
  readonly weeks: ReadonlyArray<ReadonlyArray<CalendarCell>>;
}

export type AvailabilityWindow = Readonly<{ start: string; end: string }>;
export type WindowsByDate = Readonly<
  Partial<Record<string, ReadonlyArray<AvailabilityWindow>>>
>;

export const utcToLocalDate = (utcIso: string): string => {
  const parsed = new Date(utcIso);
  return `${parsed.getFullYear()}-${String(parsed.getMonth() + JS_MONTH_OFFSET).padStart(PAD_WIDTH, '0')}-${String(parsed.getDate()).padStart(PAD_WIDTH, '0')}`;
};

export const floorHalfHour = (date: Readonly<Date>): string => {
  const hour = date.getHours();
  let minute = MINUTES_PER_HALF_HOUR;
  if (date.getMinutes() < MINUTES_PER_HALF_HOUR) {
    minute = ON_THE_HOUR_MINUTES;
  }
  return `${String(hour).padStart(PAD_WIDTH, '0')}:${String(minute).padStart(PAD_WIDTH, '0')}:00`;
};

export const ceilHalfHour = (date: Readonly<Date>): string => {
  const total = date.getHours() * MINUTES_PER_HOUR + date.getMinutes(),
    ceiled = Math.ceil(total / MINUTES_PER_HALF_HOUR) * MINUTES_PER_HALF_HOUR,
    hour = Math.min(Math.floor(ceiled / MINUTES_PER_HOUR), HOURS_PER_DAY),
    minute = ceiled % MINUTES_PER_HOUR;
  return `${String(hour).padStart(PAD_WIDTH, '0')}:${String(minute).padStart(PAD_WIDTH, '0')}:00`;
};

const compareDateStrings = (first: string, second: string): number =>
    first.localeCompare(second),
  hasWindowsOn = (windowsByDate: WindowsByDate, date: string): boolean =>
    (windowsByDate[date]?.length ?? EMPTY_COUNT) > EMPTY_COUNT,
  mergedBounds = (
    existing: Readonly<{ earliest: Date; latest: Date }> | undefined,
    start: Readonly<Date>,
    end: Readonly<Date>,
  ): { earliest: Date; latest: Date } => {
    if (!existing) {
      return { earliest: new Date(start), latest: new Date(end) };
    }
    let { earliest } = existing,
      { latest } = existing;
    if (start < existing.earliest) {
      earliest = new Date(start);
    }
    if (end > existing.latest) {
      latest = new Date(end);
    }
    return { earliest, latest };
  },
  collectDayBounds = (
    shows: ReadonlyArray<Readonly<Show>>,
  ): Partial<Record<string, { earliest: Date; latest: Date }>> => {
    let dayBounds: Partial<Record<string, { earliest: Date; latest: Date }>> =
      {};
    for (const show of shows) {
      for (const showTime of show.showTimes) {
        const start = new Date(showTime),
          end = new Date(
            start.getTime() + show.lengthInMinutes * MS_PER_MINUTE,
          ),
          dateStr = utcToLocalDate(showTime);
        dayBounds = {
          ...dayBounds,
          [dateStr]: mergedBounds(dayBounds[dateStr], start, end),
        };
      }
    }
    return dayBounds;
  };

export const buildFestivalDays = (
  shows: ReadonlyArray<Readonly<Show>>,
  windowsByDate: WindowsByDate,
): Array<FestivalDay> => {
  const dayBounds = collectDayBounds(shows);
  return Object.entries(dayBounds)
    .filter((entry): entry is [string, { earliest: Date; latest: Date }] => {
      const [, bounds] = entry;
      return Boolean(bounds);
    })
    .sort(([first], [second]) => compareDateStrings(first, second))
    .map(([date, bounds]) => ({
      date,
      earliest: floorHalfHour(
        new Date(
          bounds.earliest.getTime() - MINUTES_PER_HALF_HOUR * MS_PER_MINUTE,
        ),
      ),
      hasWindows: hasWindowsOn(windowsByDate, date),
      latest: ceilHalfHour(
        new Date(
          bounds.latest.getTime() + MINUTES_PER_HALF_HOUR * MS_PER_MINUTE,
        ),
      ),
    }));
};

const emptyCell = (): CalendarCell => ({
    date: null,
    dayNumber: null,
    festivalDay: null,
    isFestival: false,
  }),
  leadingBlankCells = (firstOfMonth: Readonly<Date>): Array<CalendarCell> => {
    const cells: Array<CalendarCell> = [];
    for (let pad = LOOP_START; pad < firstOfMonth.getDay(); pad += STEP) {
      cells.push(emptyCell());
    }
    return cells;
  },
  monthDayCells = (
    params: Readonly<{
      year: number;
      month: number;
      daysInMonth: number;
      festivalByDate: Readonly<Partial<Record<string, Readonly<FestivalDay>>>>;
    }>,
  ): Array<CalendarCell> => {
    const { year, month, daysInMonth, festivalByDate } = params,
      cells: Array<CalendarCell> = [];
    for (
      let dayNumber = FIRST_DAY_OF_MONTH;
      dayNumber <= daysInMonth;
      dayNumber += STEP
    ) {
      const date = `${year}-${String(month).padStart(PAD_WIDTH, '0')}-${String(dayNumber).padStart(PAD_WIDTH, '0')}`,
        festivalDay = festivalByDate[date] ?? null;
      cells.push({
        date,
        dayNumber,
        festivalDay,
        isFestival: Boolean(festivalDay),
      });
    }
    return cells;
  },
  withTrailingBlanks = (
    cells: ReadonlyArray<Readonly<CalendarCell>>,
  ): Array<CalendarCell> => {
    const padded = [...cells];
    while (padded.length % DAYS_PER_WEEK !== EMPTY_COUNT) {
      padded.push(emptyCell());
    }
    return padded;
  },
  buildMonthCells = (
    year: number,
    month: number,
    festivalByDate: Readonly<Partial<Record<string, Readonly<FestivalDay>>>>,
  ): Array<CalendarCell> => {
    const firstOfMonth = new Date(
        year,
        month - JS_MONTH_OFFSET,
        FIRST_DAY_OF_MONTH,
      ),
      daysInMonth = new Date(year, month, LAST_DAY_SENTINEL).getDate(),
      cells = [
        ...leadingBlankCells(firstOfMonth),
        ...monthDayCells({ daysInMonth, festivalByDate, month, year }),
      ];
    return withTrailingBlanks(cells);
  };

export const buildCalendarMonths = (
  days: ReadonlyArray<Readonly<FestivalDay>>,
): Array<CalendarMonth> => {
  if (days.length === EMPTY_COUNT) {
    return [];
  }
  const festivalByDate: Partial<Record<string, Readonly<FestivalDay>>> =
      Object.fromEntries(days.map((day) => [day.date, day])),
    uniqueMonths = [
      ...new Set(days.map((day) => day.date.slice(LOOP_START, DAYS_PER_WEEK))),
    ];

  return uniqueMonths.map((yearMonth) => {
    const [year, month] = yearMonth.split('-').map(Number),
      cells = buildMonthCells(year, month, festivalByDate),
      weeks: Array<Array<CalendarCell>> = [];
    for (let start = LOOP_START; start < cells.length; start += DAYS_PER_WEEK) {
      weeks.push(cells.slice(start, start + DAYS_PER_WEEK));
    }
    return {
      label: new Date(
        year,
        month - JS_MONTH_OFFSET,
        FIRST_DAY_OF_MONTH,
      ).toLocaleDateString('en-CA', { month: 'long', year: 'numeric' }),
      weeks,
    };
  });
};
