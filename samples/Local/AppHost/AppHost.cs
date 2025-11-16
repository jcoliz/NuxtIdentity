var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NuxtIdentity_Samples_Local_Backend>("backend")
    .WithHttpHealthCheck("/health");

builder.AddNpmApp("frontend", "../Frontend")
    .WithReference(backend)
    .WaitFor(backend)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
