# Premium Ludo WebGL + Vercel

This folder is the free website wrapper for your Unity WebGL build.

## Folder layout

- `/` -> landing page
- `/ludo/` -> browser game page
- `/ludo/unity/` -> Unity WebGL build output

When Unity exports WebGL, the important build output is:

- `index.html`
- `Build/`
- `TemplateData/`

For this setup:

- Unity's generated `index.html` is not used by the website wrapper.
- Unity's generated `index.html` and `TemplateData/` are automatically removed as redundant.
- The important files are the generated `Build/` folder, `build-manifest.json`, and `StreamingAssets/` if it exists inside `website/ludo/unity/`.
- The custom `/ludo/index.html` page loads the Unity build through `build-manifest.json`.

## How to build from Unity

1. Open the Unity project.
2. Open `File -> Build Profiles`.
3. Select `WebGL`.
4. Click `Switch Platform`.
5. Optional but recommended: run `Premium Ludo -> Build -> Prepare Fast WebGL Settings`.
6. Run `Premium Ludo -> Build -> WebGL Website Build`.

That exports the browser build into:

`website/ludo/unity`

## What the build script configures

- IL2CPP
- .NET Standard
- Strip Engine Code
- High managed stripping
- Gzip compression
- Data caching enabled
- Decompression fallback enabled
- WebGL exceptions disabled
- Low quality preset for faster runtime

## Mobile-friendly notes

- The website wrapper uses a portrait-first layout
- Touch input works through Unity UI and the browser wrapper uses `touch-action: manipulation`
- The loader caps device pixel ratio on phones to reduce GPU cost

## Deploy to Vercel for free

1. Push this repo to GitHub.
2. Go to [https://vercel.com](https://vercel.com).
3. Import your GitHub repo.
4. In Vercel project settings:
   - Framework Preset: `Other`
   - Root Directory: `website`
5. Click `Deploy`.
6. Your site will be live at:

`https://your-project-name.vercel.app`

## Future-ready structure

You can later add more tools or pages in this same website folder, for example:

- `/fixmyfile/`
- `/games/`
- `/about/`

without changing the Unity project structure.
