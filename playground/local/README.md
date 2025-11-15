# Local auth playground provider

This sample is meant to be used with the @sidebase/nuxt-auth local playground.

https://nuxt.com/modules/sidebase-auth#module-playground

1. Clone @sidebase/nuxt-auth repo locally

```
git clone https://github.com/sidebase/nuxt-auth

cd nuxt-auth

cd playground-local

pnpm i
```

2. Change the nuxt config

```diff
--- a/playground-local/nuxt.config.ts
+++ b/playground-local/nuxt.config.ts
@@ -5,6 +5,7 @@ export default defineNuxtConfig({
     transpile: ['jsonwebtoken']
   },
   auth: {
+    baseURL: 'http://localhost:3001/api/auth',
     provider: {
       type: 'local',
       endpoints: {
@@ -26,6 +27,7 @@ export default defineNuxtConfig({
         // We do an environment variable for E2E testing both options.    
         isEnabled: process.env.NUXT_AUTH_REFRESH_ENABLED !== 'false',     
         endpoint: { path: '/refresh', method: 'post' },
+        refreshOnlyToken: false, // rotate refresh tokens!
         token: {
           signInResponseRefreshTokenPointer: '/token/refreshToken',       
           refreshResponseTokenPointer: '',
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

5. Register a new user

Visit http://localhost:3000/register. Enter a username and password, then click "sign up".

This will create a new user in the backend.

6. Log in with that user
  
Click "navigate to login page"

Enter the user name and password from previous step.