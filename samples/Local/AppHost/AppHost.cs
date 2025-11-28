var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NuxtIdentity_Samples_Local_Backend>("backend")
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("frontend", "../Frontend")
    .WithPnpm()
    .WithReference(backend)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("NUXT_LAZY", "false")  // Disable lazy loading
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

await builder.Build().RunAsync();
