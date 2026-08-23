import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Accessibility', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('semantic properties reach the generated XAML', async ({ page }) => {
    await designer.addControl('Image');
    await designer.selectFirst('Image');

    await page.getByTestId('prop-semanticDescription').fill('Team photo');
    await page.getByTestId('prop-semanticHint').fill('Opens the profile');
    await page.getByTestId('prop-semanticHeadingLevel').selectOption('Level1');

    await designer.expectXamlToContain(
      'SemanticProperties.Description="Team photo"',
      'SemanticProperties.Hint="Opens the profile"',
      'SemanticProperties.HeadingLevel="Level1"'
    );
  });

  test('an image without a description is flagged', async ({ page }) => {
    await designer.addControl('Image');
    await designer.selectFirst('Image');

    await expect(page.getByTestId('a11y-issue-missing-description')).toBeVisible();

    await page.getByTestId('prop-semanticDescription').fill('Team photo');

    await expect(page.getByTestId('a11y-issue-missing-description')).toHaveCount(0);
  });

  test('faint text is reported with its measured ratio', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('prop-textColor').fill('#eeeeee');

    await expect(page.getByTestId('a11y-issue-low-contrast')).toBeVisible();
    await expect(page.getByTestId('contrast-ratio')).toContainText(':1');

    await page.getByTestId('prop-textColor').fill('#000000');

    await expect(page.getByTestId('a11y-issue-low-contrast')).toHaveCount(0);
  });

  test('hand written semantic properties survive a round trip', async () => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Image x:Name="Photo" SemanticProperties.Description="Team photo" SemanticProperties.HeadingLevel="Level2" />
  </AbsoluteLayout>
</ContentPage>`);

    await designer.expectXamlToContain(
      'SemanticProperties.Description="Team photo"',
      'SemanticProperties.HeadingLevel="Level2"'
    );
  });

  test('no contrast verdict is shown for an element with no text', async ({ page }) => {
    await designer.addControl('Grid');
    await designer.selectFirst('Grid');

    // Reporting a ratio here would be inventing one -- there is nothing to judge.
    await expect(page.getByTestId('contrast-ratio')).toHaveCount(0);
    await expect(page.getByTestId('a11y-issue-low-contrast')).toHaveCount(0);
  });
});
