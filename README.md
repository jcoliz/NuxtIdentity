# Nuxt Identity

The **Nuxt Identity** project aims to be the ASP.NET developer's companion to [@sidebase/nuxt-auth](https://auth.sidebase.io/). If you're developing a web application with a Nuxt frontend, and an ASP.NET backend, the **Nuxt Identity** project will provide .NET libraries you can add to your application to get started quickly. Built on ASP.NET Core Identity, this project bridges the gap between ASP.NET and Nuxt for auth and identity. 

## Why?

Why are we doing this instead of using something that's already out there?

- 🎯 **Specific niche:** ASP.NET Core Identity works great, but it doesn't "speak nuxt-auth" out of the box
- 🧹 **Reduces boilerplate:** Developers won't need to figure out JWT token formats, refresh token flows, and endpoint structures that nuxt-auth expects
- 🔌 **Pre-configured endpoints:** Will provide drop-in-ready API controllers that match what nuxt-auth providers expect
- 🔒 **Type safety bridge:** Will include TypeScript types for the frontend that match the backend .NET models

## What's coming?

- 📦 **NuGet packages** developers can drop in
- 🔌 **Pre-built endpoints** matching nuxt-auth's credential/refresh token providers
- 📚 **Clear examples** for both .NET and Nuxt sides
- ⚡ **Minimal config** - sensible defaults that "just work"
- 🔐 **Security best practices** baked in
