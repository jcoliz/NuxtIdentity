var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NuxtIdentity_Samples_Local_Backend>("backend")
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("frontend", "../Frontend")
    .WithPnpm()
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("NUXT_LAZY", "false")  // Disable lazy loading
    .WithEnvironment("NUXT_PUBLIC_API_BASE_URL", backend.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

await builder.Build().RunAsync();
