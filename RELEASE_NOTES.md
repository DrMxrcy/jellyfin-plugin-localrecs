## v0.8.0

### New Features
- **Per-user item exclusions**: explicitly exclude specific movies or TV shows from your recommendations via `/web/configurationpage?name=LocalRecommendationsPreferences`
- **Per-user genre weights**: boost (up to 3×) or suppress (down to 0.1×) recommendations from specific genres
- New `GET/POST/DELETE /LocalRecs/Preferences/Exclusions` and `PUT /LocalRecs/Preferences/GenreWeights` API endpoints accessible to any authenticated user

### Infrastructure
- Automated releases: push a `v*.*.*` git tag to build, test, package, update `manifest.json`, and publish a GitHub Release automatically
