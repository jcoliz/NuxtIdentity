var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NuxtIdentity_Samples_Local_Backend>("backend")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
