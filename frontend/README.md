# React + TypeScript + Vite

## HimLedger identity and workspaces

The API accepts Entra ID v2 access tokens when `EntraId__Authority` and
`EntraId__Audience` are configured. Set the authority to the organization's
tenant authority (for example, `https://login.microsoftonline.com/<tenant-id>/v2.0`)
and the audience to the API registration's audience. The API maps the validated
token's `tid` and `oid` to an internal user; an administrator must provision the
internal user, department, role, and Entra identity before that person can use
the API. Manage mappings with the admin-only `PUT /api/Users/{id}/entra-identity`
and department assignments with `PUT /api/Users/{id}/department`.

For the browser app, configure `VITE_ENTRA_CLIENT_ID`, `VITE_ENTRA_AUTHORITY`,
and `VITE_ENTRA_API_SCOPE` (the exposed API scope, such as
`api://<api-client-id>/access_as_user`). No client secret belongs in the
frontend. Local password login is only enabled in development unless
`VITE_ENABLE_LOCAL_LOGIN=true` is explicitly set.

Authenticated users are routed to `/employee/*`, `/manager/*`, `/finance/*`,
or `/admin/*` based on their server-returned role. These routes are only a
presentation boundary; the API independently enforces every role and resource
scope.

To regenerate TypeScript API models from the running API's OpenAPI document,
start the API on `http://localhost:5228` and run `npm run api:types`.

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```

You can also install [eslint-plugin-react-x](https://npmx.dev/package/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://npmx.dev/package/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```
