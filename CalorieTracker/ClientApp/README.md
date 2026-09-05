# CalorieTracker food-search island

This directory contains the React/Vite island used by the Foods and Diary pages for USDA food search. USDA requests and all authoritative validation remain on the ASP.NET Core server.

Install dependencies and run the development server with:

```bash
npm install
npm run dev
```

The production build is written to `../wwwroot/react-food-search`, where the Razor pages load the generated JavaScript and CSS:

```bash
npm run lint
npm run build
```

The generated production assets are committed because the .NET project does not invoke Vite automatically. Rebuild them whenever the React source or Vite configuration changes.
