//
// Health Check Endpoint
//
// This endpoint provides a simple health check for the Nuxt.js frontend service.
// It returns a JSON object indicating the service status and other relevant information.
// Used by .NET Aspire to report the health of the frontend service.
//

export default defineEventHandler(async (event) => {
  const healthStatus = {
    status: 'healthy',
    timestamp: new Date().toISOString(),
    service: 'nuxt-frontend',
    version: process.env.npm_package_version || '1.0.0',
    environment: process.env.NODE_ENV || 'development'
  }

  setHeader(event, 'Content-Type', 'application/json')
  return healthStatus
})