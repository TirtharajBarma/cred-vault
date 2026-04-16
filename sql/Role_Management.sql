-- ============================================================================
-- CredVault Role Management Script
-- Purpose: SQL queries to manage user roles
-- Usage: Run these queries directly in SQL Server Management Studio or via sqlcmd
-- ============================================================================

-- ============================================================================
-- VIEW ALL USERS AND THEIR ROLES
-- ============================================================================
PRINT '=== ALL USERS ==='
SELECT 
    id,
    email,
    role AS role_int,
    CASE role 
        WHEN 0 THEN 'User'
        WHEN 1 THEN 'Admin'
        ELSE 'Unknown'
    END AS role_name,
    status AS status_int,
    CASE status 
        WHEN 0 THEN 'PendingVerification'
        WHEN 1 THEN 'Active'
        WHEN 2 THEN 'Suspended'
        WHEN 3 THEN 'Blocked'
        WHEN 4 THEN 'Deleted'
        ELSE 'Unknown'
    END AS status_name
FROM credvault_identity.dbo.identity_users
ORDER BY role, email;

-- ============================================================================
-- MAKE A USER AN ADMIN
-- ============================================================================
-- Replace 'user@example.com' with the actual email
DECLARE @UserEmail NVARCHAR(256) = 'user@example.com';

UPDATE credvault_identity.dbo.identity_users
SET role = 1  -- 1 = Admin
WHERE email = @UserEmail;

PRINT 'User ' + @UserEmail + ' is now an Admin';

-- ============================================================================
-- MAKE AN ADMIN A REGULAR USER
-- ============================================================================
-- Replace 'admin@example.com' with the actual email
DECLARE @AdminEmail NVARCHAR(256) = 'admin@example.com';

UPDATE credvault_identity.dbo.identity_users
SET role = 0  -- 0 = User
WHERE email = @AdminEmail;

PRINT 'User ' + @AdminEmail + ' is now a regular User';

-- ============================================================================
-- BATCH: MAKE ALL ADMINS REGULAR USERS (use with caution!)
-- ============================================================================
-- UPDATE credvault_identity.dbo.identity_users
-- SET role = 0  -- 0 = User
-- WHERE role = 1;  -- Current Admins

-- ============================================================================
-- BATCH: PROMOTE SPECIFIC USERS TO ADMIN (use with caution!)
-- ============================================================================
-- UPDATE credvault_identity.dbo.identity_users
-- SET role = 1  -- 1 = Admin
-- WHERE email IN ('user1@test.com', 'user2@test.com', 'user3@test.com');

-- ============================================================================
-- FIND ALL ADMINS
-- ============================================================================
PRINT '=== CURRENT ADMINS ==='
SELECT id, email, role FROM credvault_identity.dbo.identity_users WHERE role = 1;

-- ============================================================================
-- FIND ALL REGULAR USERS
-- ============================================================================
PRINT '=== CURRENT REGULAR USERS ==='
SELECT id, email, role FROM credvault_identity.dbo.identity_users WHERE role = 0;

GO
