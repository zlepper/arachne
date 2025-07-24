# Agent Task List: On-the-Fly Read-Only SQL Execution

### Objective
Implement a function to execute user SQL queries within a temporary, secure, read-only application role.

---

### **Part 1: One-Time Permission Setup**

**Task:** Grant the following required permissions to the application's SQL login one time.

```sql
-- Grant permissions to the application's login
GRANT CREATE APPLICATION ROLE TO YourAppLogin;
GRANT ALTER ANY APPLICATION ROLE TO YourAppLogin;
GRANT ALTER ANY ROLE TO YourAppLogin;
```

---

### **Part 2: Per-Query Execution Logic**

Implement a function that performs the following steps for each query. Steps 5 and 6 must be guaranteed to run (e.g., in a `finally` block).

1.  **Generate Credentials:** Create a new, secure random password for a temporary role (e.g., `TempReadOnlyExecutor`).

2.  **Create & Configure Role:** Execute SQL to `CREATE` the `APPLICATION ROLE` with the new password, then `ALTER ROLE` to add it as a member of `db_datareader`.

3.  **Activate Sandbox:** Execute `sp_setapprole` using the new credentials. You must use the cookie option (`@fCreateCookie = true`) and store the returned cookie value.

4.  **Run User Query:** With the role active, execute the user's SQL.

5.  **Revert Context:** Execute `sp_unsetapprole`, passing the stored cookie to exit the sandbox.

6.  **Destroy Role:** Execute `DROP APPLICATION ROLE` to remove the temporary role.

---

### **Reference: `sp_setapprole` Documentation Snippet**

`sp_setapprole` activates the permissions of an application role for the current database session.

#### **Syntax**

```sql
sp_setapprole [ @rolename = ] 'role',
    [ @password = ] { 'password' },
    [ @fCreateCookie = ] true | false,
    [ @cookie = ] @cookie_variable OUTPUT
```

#### **Key Arguments**

* `[ @rolename = ] 'role'`
    * The name of the application role being activated.

* `[ @password = ] 'password'`
    * The password required to activate the application role. This value is encrypted by the client before being sent to the server.

* `[ @fCreateCookie = ] true | false`
    * Specifies whether a "cookie" should be created.
    * When `true`, the procedure returns a `varbinary` value in an `OUTPUT` parameter. This cookie stores the original security context.
    * **This is the recommended practice for applications using connection pooling.**

* `[ @cookie = ] @cookie_variable OUTPUT`
    * The `OUTPUT` parameter used to store the security context cookie.
    * This cookie must be passed to `sp_unsetapprole` to revert the security context before the connection is reused.

#### **Behavior**

After `sp_setapprole` is executed successfully, the permissions of the original user for the current database are suspended. The session gains the permissions of the application role. To revert to the original security context, you must either disconnect or use `sp_unsetapprole` with the cookie.
