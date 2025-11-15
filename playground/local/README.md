# Local auth playground provider

This sample is meant to be used with the @sidebase/nuxt-auth local playground.

https://nuxt.com/modules/sidebase-auth#module-playground

1. Clone @sidebase/nuxt-auth repo locally

```
> git clone https://github.com/sidebase/nuxt-auth

> cd nuxt-auth

> cd playground-local

> pnpm i
```

2. Change the nuxt config

```diff
--- a/playground-local/nuxt.config.ts
+++ b/playground-local/nuxt.config.ts
@@ -4,7 +4,11 @@ export default defineNuxtConfig({
   build: {
     transpile: ['jsonwebtoken']
   },
+  runtimeConfig: {
+    baseURL: 'http://localhost:3001/api/auth'
+  },
   auth: {
+    originEnvKey: 'NUXT_BASE_URL',
     provider: {
       type: 'local',
       endpoints: {
```

3. Start the static frontend

This will start the frontend listening on http://localhost:3000/

```
pnpm generate

pnpm start
```

4. Run the .NET Web API in this folder

This will start the backend listening on http://localhost:3001/, which will fulfill the auth requests made by the local playground frontend.

```
dotnet run
```