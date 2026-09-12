using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FifthBox.ServerManager.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessGrants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EnrolledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PemChain = table.Column<string>(type: "TEXT", nullable: true),
                    PrivateKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NotAfter = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLogins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLogins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RootDomain = table.Column<string>(type: "TEXT", maxLength: 253, nullable: true),
                    AcmeEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    ManagerPrefix = table.Column<string>(type: "TEXT", maxLength: 63, nullable: true),
                    AcmeAccountKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    AcmeAccountDirectory = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AppliedProxyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ProxyAppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Registries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordEnc = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 253, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    WorkloadId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpstreamHost = table.Column<string>(type: "TEXT", maxLength: 253, nullable: true),
                    UpstreamScheme = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TargetPort = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    WebSockets = table.Column<bool>(type: "INTEGER", nullable: false),
                    BasicAuthEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BasicAuthUsername = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BasicAuthPasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Roles = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkloadGroups",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ParentId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkloadGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workloads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DesiredState = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Image = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Replicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Ports = table.Column<string>(type: "TEXT", nullable: false),
                    Placement = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Auto"),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PlacedNodeId = table.Column<string>(type: "TEXT", nullable: true),
                    MemoryLimitMb = table.Column<int>(type: "INTEGER", nullable: true),
                    CpuLimit = table.Column<double>(type: "REAL", nullable: true),
                    MemoryReserveMb = table.Column<int>(type: "INTEGER", nullable: true),
                    CpuReserve = table.Column<double>(type: "REAL", nullable: true),
                    Mounts = table.Column<string>(type: "TEXT", nullable: false),
                    Command = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Args = table.Column<string>(type: "TEXT", nullable: false),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RestartPolicy = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "OnFailure"),
                    StopGraceSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 10),
                    StopCommand = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ManagedDirectory = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    RestartDailyAtMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    LastScheduledRestartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    HealthCommand = table.Column<string>(type: "TEXT", nullable: true),
                    HealthIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthStartPeriodSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Env = table.Column<string>(type: "TEXT", nullable: false),
                    Revisions = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workloads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_Scope_TargetId",
                table: "AccessGrants",
                columns: new[] { "Scope", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_UserId_Scope_TargetId",
                table: "AccessGrants",
                columns: new[] { "UserId", "Scope", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_Hostname",
                table: "Certificates",
                column: "Hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_Provider_ProviderKey",
                table: "ExternalLogins",
                columns: new[] { "Provider", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_UserId",
                table: "ExternalLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Code",
                table: "Invites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Registries_Domain",
                table: "Registries",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Hostname_Path",
                table: "Routes",
                columns: new[] { "Hostname", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_WorkloadId",
                table: "Routes",
                column: "WorkloadId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Email",
                table: "UserProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadGroups_ParentId",
                table: "WorkloadGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Workloads_AgentId",
                table: "Workloads",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Workloads_GroupId",
                table: "Workloads",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Workloads_Name",
                table: "Workloads",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessGrants");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "EnrollmentKeys");

            migrationBuilder.DropTable(
                name: "ExternalLogins");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Registries");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkloadGroups");

            migrationBuilder.DropTable(
                name: "Workloads");
        }
    }
}
