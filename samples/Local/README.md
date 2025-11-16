# Local provider sample

This sample is a .NET Aspire app demonstrating minimal NuxtIdentity functionality.

## Frontend

* Running Nuxt 4
* Generated code
* With @sidebase/nuxt-auth
* Recommended configuration of local provider for calling a backend running NuxtIdentity.
* Good-looking professional-quality UI
* Demonstrates only the auth capabilities of the backend
* Custom UI we control, so don't have to rely on external repo for front end

### UI pages

* Home: Acessible by any user
* Register: Only accessible if logged out. UI to call /signup
* Login: Only accessible if logged out. UI to call /login
* Profile: Only accessible if logged in. Shows /session. Also can call /logout from here
* Header: On every page, Shows logged in status, auth token, refresh token. Also links to the other pages.

## Backend

* .NET Web API
* Using NuxtIdentity
* Bare minimum functionality, just the auth backend APIs

## User can:

* Sign up
* Log in
* View session
* Log out
* Stay signed in (refreshing token)
