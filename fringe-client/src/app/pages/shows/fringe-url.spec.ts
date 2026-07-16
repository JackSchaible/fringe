import { fringeUrl } from './fringe-url';

const FRINGE_URL_TEST_ID = 42;

describe('fringeUrl', () => {
  it('returns correct Fringe ticket URL', () => {
    expect(fringeUrl(FRINGE_URL_TEST_ID)).toBe(
      `https://tickets.fringetheatre.ca/event/601:${FRINGE_URL_TEST_ID}`,
    );
  });
});
