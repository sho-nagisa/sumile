using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class RepairAspNetUsersColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';

                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "UserType" text NOT NULL DEFAULT 'Normal';

                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "CustomId" integer NOT NULL DEFAULT 0;

                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "Gender" integer NOT NULL DEFAULT 0;

                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "IsAdmin" boolean NOT NULL DEFAULT false;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'ShiftRole'
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'UserShiftRole'
                    ) THEN
                        ALTER TABLE "AspNetUsers" RENAME COLUMN "ShiftRole" TO "UserShiftRole";
                    END IF;
                END $$;

                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "UserShiftRole" integer NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
