using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sumile.Data;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260629062000_RepairShiftSubmissionsColumns")]
    public partial class RepairShiftSubmissionsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ShiftSubmissions'
                          AND column_name = 'UserType'
                    ) THEN
                        ALTER TABLE "ShiftSubmissions"
                        ADD COLUMN "UserType" integer NOT NULL DEFAULT 0;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ShiftSubmissions'
                          AND column_name = 'UserType'
                          AND data_type = 'text'
                    ) THEN
                        UPDATE "ShiftSubmissions"
                        SET "UserType" = CASE "UserType"
                            WHEN 'Normal' THEN '0'
                            WHEN 'Admin' THEN '1'
                            WHEN 'AdminUpdated' THEN '2'
                            ELSE '0'
                        END;

                        ALTER TABLE "ShiftSubmissions"
                        ALTER COLUMN "UserType" DROP DEFAULT;

                        ALTER TABLE "ShiftSubmissions"
                        ALTER COLUMN "UserType" TYPE integer USING "UserType"::integer;

                        ALTER TABLE "ShiftSubmissions"
                        ALTER COLUMN "UserType" SET DEFAULT 0;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ShiftSubmissions'
                          AND column_name = 'ShiftRole'
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ShiftSubmissions'
                          AND column_name = 'UserShiftRole'
                    ) THEN
                        ALTER TABLE "ShiftSubmissions" RENAME COLUMN "ShiftRole" TO "UserShiftRole";
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'ShiftSubmissions'
                          AND column_name = 'UserShiftRole'
                    ) THEN
                        ALTER TABLE "ShiftSubmissions"
                        ADD COLUMN "UserShiftRole" integer NOT NULL DEFAULT 0;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
