# Premium Ludo WebGL Deployment Guide

This project is now prepared for a fast, beginner-friendly Unity WebGL deployment flow.

## 1. Switch Unity to WebGL

Inside Unity:

1. Open `File -> Build Profiles`
2. Select `WebGL`
3. Click `Switch Platform`

If Unity asks for WebGL Build Support and it is missing:

1. Open Unity Hub
2. Go to `Installs`
3. Find `6000.4.0f1`
4. Add module: `WebGL Build Support`

## 2. Apply fast WebGL settings

This project now includes:

- `Premium Ludo -> Build -> Prepare Fast WebGL Settings`
- `Premium Ludo -> Build -> WebGL Website Build`

Use them in this order:

1. `Premium Ludo -> Build -> Prepare Fast WebGL Settings`
2. `Premium Ludo -> Build -> WebGL Website Build`

The build goes to:

`website/ludo/unity`

## 3. WebGL output structure

Unity creates:

- `index.html`
- `Build/`
- `TemplateData/`

For this website setup:

- the important runtime files are `Build/`, `build-manifest.json`, and `StreamingAssets/` only when that folder actually exists
- Unity's generated `index.html` is not the page users visit
- Unity's generated `index.html` and `TemplateData/` are removed automatically as redundant
- your custom wrapper page is:
  `website/ludo/index.html`

The build script also generates:

- `build-manifest.json`

That file lets the wrapper auto-detect the correct Unity loader/data/framework/wasm filenames.

## 4. Website pages

This repo now includes:

- `website/index.html`
  Landing page with a `Play Ludo` button
- `website/ludo/index.html`
  The actual Unity game page
- `website/assets/site.css`
  Responsive styling
- `website/assets/ludo-loader.js`
  Fast loader with smoothed progress and fullscreen support

## 5. Performance defaults applied

The WebGL setup now aims for low-latency browser performance:

- IL2CPP
- .NET Standard
- Strip Engine Code
- High managed stripping
- WebGL exceptions disabled
- Gzip compression
- Data caching enabled
- Decompression fallback enabled
- WebGL file hashing enabled
- WebGL initial memory: `64 MB`
- WebGL maximum memory: `512 MB`
- WebGL runtime quality forced to `Low`
- `Application.targetFrameRate = 60`
- `VSync = 0`

## 6. Vercel deployment

1. Commit and push your repo to GitHub
2. Open [https://vercel.com](https://vercel.com)
3. Import the GitHub repo
4. Set:
   - Framework Preset: `Other`
   - Root Directory: `website`
5. Deploy

Your live URL will look like:

`https://your-project-name.vercel.app`

## 7. Vercel caching

This project now includes:

`website/vercel.json`

It adds aggressive cache headers for Unity build files:

- long-term immutable caching for `/ludo/unity/Build/*`
- shorter caching for the wrapper site assets

## 8. Beginner deployment checklist

Before pushing to Vercel:

1. Build WebGL from Unity
2. Confirm these folders exist:
   - `website/ludo/unity/Build`
3. Confirm this file exists:
   - `website/ludo/unity/build-manifest.json`
4. Commit the `website/ludo/unity` build files to GitHub
5. Deploy on Vercel

## 9. Mobile responsiveness

The wrapper is already prepared for mobile:

- portrait-first canvas area
- capped device pixel ratio on phones for better FPS
- touch-friendly buttons
- fullscreen support
- simplified loading UI

## 10. Performance checklist after deployment

After the site is live, test these:

- page opens quickly
- loading bar appears immediately
- board interaction feels responsive
- dice tap has no visible delay
- no large jank when the game first starts
- mobile browser does not overheat quickly
- repeat visits load faster because of caching

## 11. Easy future expansion

The website structure is ready for more tools later:

- `website/fixmyfile/`
- `website/games/`
- `website/about/`

That means you can keep using one Vercel project and expand the site over time.
