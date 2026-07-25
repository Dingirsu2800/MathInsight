/*
==========================================================
MathInsight Permission / RolePermission seed
Run after: 002_Seed_MathInsight_Demo.sql (Role rows must already exist)

Seeds the Permission Matrix from specs/001-identity-access/spec.md plus the
`identity:admin_access` guard permission (UC-16: an Admin cannot remove this
key from the Admin role while editing their own role).

The script is idempotent and may be run again on the same database.
==========================================================
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @AdminRoleId   VARCHAR(36) = (SELECT [RoleID] FROM [Role] WHERE [RoleName] = N'Admin');
DECLARE @ExpertRoleId  VARCHAR(36) = (SELECT [RoleID] FROM [Role] WHERE [RoleName] = N'Expert');
DECLARE @TeacherRoleId VARCHAR(36) = (SELECT [RoleID] FROM [Role] WHERE [RoleName] = N'Teacher');
DECLARE @StudentRoleId VARCHAR(36) = (SELECT [RoleID] FROM [Role] WHERE [RoleName] = N'Student');

DECLARE @LoginLogoutId    VARCHAR(36) = '55555555-0001-0001-0001-000000000001';
DECLARE @RegisterId       VARCHAR(36) = '55555555-0001-0001-0001-000000000002';
DECLARE @VerifyTeacherId  VARCHAR(36) = '55555555-0001-0001-0001-000000000003';
DECLARE @DeactivateId     VARCHAR(36) = '55555555-0001-0001-0001-000000000004';
DECLARE @ImportBatchId    VARCHAR(36) = '55555555-0001-0001-0001-000000000005';
DECLARE @AdjustPermsId    VARCHAR(36) = '55555555-0001-0001-0001-000000000006';
DECLARE @AdminAccessId    VARCHAR(36) = '55555555-0001-0001-0001-000000000007';

MERGE [Permission] AS target
USING (VALUES
    (@LoginLogoutId,   N'identity:login_logout',             N'Login / Logout'),
    (@RegisterId,      N'identity:register_account',         N'Register Account (self-service)'),
    (@VerifyTeacherId, N'identity:verify_teacher_credentials', N'Verify Teacher Credentials'),
    (@DeactivateId,    N'identity:deactivate_account',       N'Deactivate Account'),
    (@ImportBatchId,   N'identity:import_batch_accounts',    N'Import Batch Accounts'),
    (@AdjustPermsId,   N'identity:adjust_permissions',       N'Adjust Permissions'),
    (@AdminAccessId,   N'identity:admin_access',             N'Guard permission identifying the Admin role')
) AS source ([PermissionID], [PermissionKey], [Description])
ON target.[PermissionKey] = source.[PermissionKey]
WHEN MATCHED THEN
    UPDATE SET
        target.[Description] = source.[Description]
WHEN NOT MATCHED THEN
    INSERT ([PermissionID], [PermissionKey], [Description])
    VALUES (source.[PermissionID], source.[PermissionKey], source.[Description]);

SELECT @LoginLogoutId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:login_logout';
SELECT @RegisterId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:register_account';
SELECT @VerifyTeacherId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:verify_teacher_credentials';
SELECT @DeactivateId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:deactivate_account';
SELECT @ImportBatchId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:import_batch_accounts';
SELECT @AdjustPermsId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:adjust_permissions';
SELECT @AdminAccessId = [PermissionID] FROM [Permission] WHERE [PermissionKey] = N'identity:admin_access';

MERGE [RolePermission] AS target
USING (VALUES
    (@AdminRoleId, @LoginLogoutId),
    (@AdminRoleId, @VerifyTeacherId),
    (@AdminRoleId, @DeactivateId),
    (@AdminRoleId, @ImportBatchId),
    (@AdminRoleId, @AdjustPermsId),
    (@AdminRoleId, @AdminAccessId),
    (@ExpertRoleId, @LoginLogoutId),
    (@TeacherRoleId, @LoginLogoutId),
    (@TeacherRoleId, @RegisterId),
    (@StudentRoleId, @LoginLogoutId),
    (@StudentRoleId, @RegisterId)
) AS source ([RoleID], [PermissionID])
ON target.[RoleID] = source.[RoleID] AND target.[PermissionID] = source.[PermissionID]
WHEN NOT MATCHED THEN
    INSERT ([RoleID], [PermissionID])
    VALUES (source.[RoleID], source.[PermissionID]);

COMMIT TRANSACTION;

SELECT
    r.[RoleName],
    p.[PermissionKey]
FROM [RolePermission] rp
JOIN [Role] r ON r.[RoleID] = rp.[RoleID]
JOIN [Permission] p ON p.[PermissionID] = rp.[PermissionID]
ORDER BY r.[RoleName], p.[PermissionKey];

GO
