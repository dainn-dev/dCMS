# DAI-713 — Umbraco DocTypes Specification

> Source-of-truth design for the 7 content types this platform owns.
> Author them in the Umbraco backoffice (Settings → Document Types) on a fresh
> dev environment; uSync's `ExportAtStartup=All` (Development) will then
> serialize them to `src/backend/dCMS.Web/uSync/v16/ContentTypes/`.

## Folder layout (after first export)

```
src/backend/dCMS.Web/uSync/v16/
  ContentTypes/
    HomepageMainBanner.config
    HomepageSubBanner.config
    ProductBlock.config
    NavigationMenu.config
    LandingPage.config
    WysiwygPage.config
    ReusableSection.config
    EmbeddedVideo.config
  DataTypes/        (auto-populated by export)
  Templates/
  Languages/
  MediaTypes/
  RelationTypes/
```

## Conventions

- **Allow as root**: only `HomepageMainBanner`, `LandingPage`, `WysiwygPage`.
- **Compositions**: `ReusableSection` is a composition; the rest are concrete.
- **Variations**: all doctypes are `Culture` variant (multi-language ready).
- **Naming**: PascalCase alias; tabs grouped as `Content`, `SEO`, `Settings`.
- **Element types** (block usage): `EmbeddedVideo`, `ProductBlock` are flagged
  "Is an Element type" (used inside Block Grid).

## DocTypes

### 1. HomepageMainBanner

Hero/main banner shown above the fold on the homepage.

| Property             | Alias              | DataType                        | Required | Notes                              |
| -------------------- | ------------------ | ------------------------------- | -------- | ---------------------------------- |
| Headline             | headline           | Textstring                      | Y        | Max 120 chars                      |
| Subheadline          | subheadline        | Textarea                        | N        | Max 240 chars                      |
| Background image     | backgroundImage    | Image Media Picker              | Y        | Single, image-only                 |
| CTA label            | ctaLabel           | Textstring                      | N        |                                    |
| CTA target           | ctaTarget          | Multinode Treepicker            | N        | Internal page link (max 1)         |
| Show overlay         | showOverlay        | True/false                      | N        | Default false                      |

Tabs: `Content` (all above).

### 2. HomepageSubBanner

Smaller banners below the hero (3-up grid).

| Property        | Alias        | DataType             | Required |
| --------------- | ------------ | -------------------- | -------- |
| Title           | title        | Textstring           | Y        |
| Image           | image        | Image Media Picker   | Y        |
| Link            | link         | Multinode Treepicker | N        |

Element type: **No** (it's a child page, not a block).

### 3. ProductBlock

Block that links one or more products from the Catalog API into a Block Grid.

Element type: **Yes**.

| Property      | Alias       | DataType                  | Required | Notes                                |
| ------------- | ----------- | ------------------------- | -------- | ------------------------------------ |
| Heading       | heading     | Textstring                | N        | Optional section heading             |
| Products      | products    | UmbracoTreePicker (custom)| Y        | Existing custom picker → Catalog API |
| Layout        | layout      | Dropdown (single)         | N        | Values: `grid`, `carousel`, `list`   |
| Columns       | columns     | Numeric                   | N        | 1–6, default 3                       |

> **`UmbracoTreePicker`** is the existing SPA component bundled in the
> backoffice (see `src/backoffice/dcms-backoffice-spa/src/estore/components/UmbracoTreePicker.tsx`).
> Wire its property editor entry-point as the value editor for `products`.

### 4. NavigationMenu

Site-wide navigation root + nestable items (recursive).

Element type: **No** (a real content node).

| Property     | Alias     | DataType                   | Required |
| ------------ | --------- | -------------------------- | -------- |
| Label        | label     | Textstring                 | Y        |
| Link         | link      | Multinode Treepicker (1)   | N        |
| Open in tab  | newTab    | True/false                 | N        |

Allow children: `NavigationMenu` (recursive).

### 5. LandingPage

Marketing landing page composed of a Block Grid of reusable sections.

Allowed at root: **Yes**.

| Property    | Alias      | DataType                | Required | Tab     |
| ----------- | ---------- | ----------------------- | -------- | ------- |
| Page title  | pageTitle  | Textstring              | Y        | Content |
| Hero        | hero       | Image Media Picker      | N        | Content |
| Body        | body       | Block Grid Editor       | Y        | Content |
| SEO title   | seoTitle   | Textstring              | N        | SEO     |
| SEO desc.   | seoDesc    | Textarea                | N        | SEO     |
| OG image    | ogImage    | Image Media Picker      | N        | SEO     |

Block Grid `body` allowed blocks: `ProductBlock`, `EmbeddedVideo`, `ReusableSection`.

### 6. WysiwygPage

Rich-text content page (legacy article-style).

Allowed at root: **Yes**.

| Property    | Alias      | DataType            | Required | Tab     |
| ----------- | ---------- | ------------------- | -------- | ------- |
| Page title  | pageTitle  | Textstring          | Y        | Content |
| Body        | body       | Richtext editor     | Y        | Content |
| SEO title   | seoTitle   | Textstring          | N        | SEO     |
| SEO desc.   | seoDesc    | Textarea            | N        | SEO     |

### 7. ReusableSection

Composition + element-type wrapper for cross-page reusable content blocks.

Element type: **Yes**.
Compositions: `ReusableSection` is itself usable as a composition.

| Property    | Alias       | DataType            | Required |
| ----------- | ----------- | ------------------- | -------- |
| Section key | sectionKey  | Textstring          | Y        |
| Body        | body        | Richtext editor     | Y        |
| Image       | image       | Image Media Picker  | N        |

### 7.5 EmbeddedVideo (helper element type)

Element type used inside Block Grid; not in the headline 7 but listed in the
DAI-713 ticket as the eighth dependency.

| Property      | Alias       | DataType            | Required |
| ------------- | ----------- | ------------------- | -------- |
| Provider      | provider    | Dropdown            | Y (`youtube`/`vimeo`) |
| Video ID      | videoId     | Textstring          | Y        |
| Caption       | caption     | Textarea            | N        |

## After authoring

1. Stop and start `dCMS.Web` in Development.
2. Confirm `src/backend/dCMS.Web/uSync/v16/ContentTypes/*.config` files appear.
3. Commit them to git.
4. Reset the dev DB; restart with `ImportAtStartup=All` and verify the doctypes
   reappear identical (round-trip check).
5. Once round-trip is clean, the same XML feeds tenant containers (DAI-714).

## CI drift gate (DAI-712 AC4)

After the above is committed, the `usync-drift` workflow can be promoted from
"folder-present" to "spawn fresh DB → export → `git diff --exit-code`".
That promotion is tracked as part of DAI-714.
