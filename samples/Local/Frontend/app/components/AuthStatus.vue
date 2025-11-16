<script setup lang="ts">

const { token, refreshToken, data, status, lastRefreshedAt } = useAuth()

</script>
<template>
  <div>
    <hr/>
    <pre>Status: <span data-testid="status">{{ status }}</span></pre>
    <pre>Data: {{ data || 'no session data present, are you logged in?' }}</pre>
    <pre>Last refreshed at: {{ lastRefreshedAt || 'no refresh happened' }}</pre>
    <pre>JWT token: {{ token || 'no token present, are you logged in?' }}</pre>
    <pre>Refresh token: {{ refreshToken || 'N/A' }}</pre>
  </div>
</template>
