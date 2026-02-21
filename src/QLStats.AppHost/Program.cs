var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

var db = postgres.AddDatabase("qlstats-db");

builder.AddProject<Projects.QLStats>("qlstats-app")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
