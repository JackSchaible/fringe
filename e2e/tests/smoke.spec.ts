import { test, expect } from "@playwright/test";

test("home page loads and renders the app shell", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle(/FringeQuest/);
  await expect(page.locator(".footer-copy")).toBeVisible();
});
