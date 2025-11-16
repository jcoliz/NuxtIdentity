// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  vite: {
    css: {
      preprocessorOptions: {
        scss: {
          quietDeps: true,
          // You can also silence specific deprecation types from your own code if needed
          silenceDeprecations: ['import', 'color-functions'],
        },
      },
    },
  },
  css: [
    '~/assets/scss/custom.scss'
  ],
  router: {
    options: {
      linkActiveClass: 'active'
    }
  },
  modules: ['@sidebase/nuxt-auth'],
  auth: {
    baseURL: 'http://localhost:5000/api/auth', // Update this to your backend URL
    provider: {
      type: 'local',
      endpoints: {
        signIn: { path: '/login', method: 'post' },
        signOut: { path: '/logout', method: 'post' },
        signUp: { path: '/signup', method: 'post' }, // Changed from /register to /signup
        getSession: { path: '/user', method: 'get' } // Changed from /profile to /user
      },
      pages: {
        login: '/login',
        logout: '/', // Redirect to home page after logout
      },
      token: {
        signInResponseTokenPointer: '/token/accessToken', // Updated to match LoginResponse structure
        type: 'Bearer',
        cookieName: 'auth.token',
        headerName: 'Authorization',
        headerType: 'Bearer',
        maxAgeInSeconds: 60 * 60 * 24 * 30 // Changed from cookieMaxAge to maxAgeInSeconds
      },
      refresh: {
        isEnabled: true,
        endpoint: { path: '/refresh', method: 'post' },
        token: {
          signInResponseRefreshTokenPointer: '/token/refreshToken'
        }
      },
      session: {
        dataType:  {
          id: 'string',
          email: 'string', 
          name: 'string',
          roles: 'string[]', // Updated to match your UserInfo model
          claims: '{ type:string, value:string }[]'
        },
        dataResponsePointer: '/' // May be incorrect?
      }
    },
    sessionRefresh: {
      // Whether to refresh the session every time the browser window is refocused.
      enableOnWindowFocus: true,
      // Whether to refresh the session every `X` milliseconds. Set this to `false` to turn it off. The session will only be refreshed if a session already exists.
      enablePeriodically: 10000, // just for demo!!
      // Custom refresh handler - uncomment to use
      // handler: './config/AuthRefreshHandler'
    },
    globalAppMiddleware: {
      isEnabled: true
    }    
  }
})
