using System;
using System.Data;
using BizSuite.Data;
using BizSuite.Models.Auth;
using BC = BCrypt.Net.BCrypt;

namespace BizSuite.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly DbHelper _db;

        public UserRepository(DbHelper db)
        {
            _db = db;
        }

        public UserLoginResult? ValidateLogin(string email, string password)
        {

            var dt = _db.ExecuteQuery("sp_GetUserAuthData", DbHelper.P("@Email", email));

            if (dt.Rows.Count == 0)
                return null;

            var row = dt.Rows[0];
            string storedHash = row["PasswordHash"].ToString() ?? "";

            bool valid = false;
            try
            {
                valid = BC.Verify(password, storedHash);
            }
            catch
            {
                valid = false;
            }

            if (!valid) return null;

            var result = new UserLoginResult
            {
                UserId = Convert.ToInt32(row["UserId"]),
                CompanyId = Convert.ToInt32(row["CompanyId"]),
                RoleName = row["RoleName"].ToString() ?? "Staff",
                FullName = row["FullName"].ToString() ?? "",
                Email = row["Email"].ToString() ?? "",
                Company = row["CompanyName"].ToString() ?? "",
                Phone = row.Table.Columns.Contains("Phone") && row["Phone"] != DBNull.Value ? row["Phone"].ToString() : "",
                Department = row.Table.Columns.Contains("Department") && row["Department"] != DBNull.Value ? row["Department"].ToString() : ""
            };

            if (row["ProfileImageData"] != DBNull.Value)
            {
                result.ProfileImageData = (byte[])row["ProfileImageData"];
                result.ProfileImageMimeType = row["ProfileImageMimeType"].ToString();
            }

            return result;
        }

        public UserLoginResult? RegisterTenant(string companyName, string fullName, string email, string password)
        {

            string hashedPassword = BC.HashPassword(password);

            var dt = _db.ExecuteQuery("sp_RegisterTenant",
                DbHelper.P("@CompanyName", companyName),
                DbHelper.P("@FullName", fullName),
                DbHelper.P("@Email", email),
                DbHelper.P("@PasswordHash", hashedPassword)
            );

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];

                if (dt.Columns.Contains("ErrorMessage"))
                {
                    throw new Exception(row["ErrorMessage"].ToString());
                }

                return new UserLoginResult
                {
                    UserId = Convert.ToInt32(row["UserId"]),
                    CompanyId = Convert.ToInt32(row["CompanyId"]),
                    FullName = row["FullName"].ToString() ?? "",
                    RoleName = row["RoleName"].ToString() ?? "Admin",
                    Email = email,
                    Company = companyName
                };
            }
            return null;
        }

        public bool ChangePassword(int userId, string oldPassword, string newPassword)
        {

            var dt = _db.ExecuteQuery("SELECT PasswordHash FROM Users WHERE UserId = @UserId AND IsDeleted = 0",
                DbHelper.P("@UserId", userId));

            if (dt.Rows.Count == 0) return false;

            string storedHash = dt.Rows[0]["PasswordHash"].ToString() ?? "";

            if (!BC.Verify(oldPassword, storedHash)) return false;

            string newHash = BC.HashPassword(newPassword);
            int rows = _db.ExecuteNonQuery("UPDATE Users SET PasswordHash = @Hash WHERE UserId = @UserId",
                DbHelper.P("@Hash", newHash),
                DbHelper.P("@UserId", userId));

            return rows > 0;
        }

        public void UpdateProfile(int userId, string fullName, string email, string phone, string department, byte[]? imageData, string? mimeType)
        {
            if (imageData != null)
            {
                _db.ExecuteNonQuery(@"
                    UPDATE Users SET 
                        FullName = @Name, 
                        Email = @Email, 
                        Phone = @Phone, 
                        Department = @Dept,
                        ProfileImageData = @Img,
                        ProfileImageMimeType = @Mime
                    WHERE UserId = @Id AND IsDeleted = 0",
                    DbHelper.P("@Name", fullName),
                    DbHelper.P("@Email", email),
                    DbHelper.P("@Phone", phone),
                    DbHelper.P("@Dept", department),
                    DbHelper.P("@Img", imageData),
                    DbHelper.P("@Mime", mimeType),
                    DbHelper.P("@Id", userId));
            }
            else
            {
                _db.ExecuteNonQuery(@"
                    UPDATE Users SET 
                        FullName = @Name, 
                        Email = @Email, 
                        Phone = @Phone, 
                        Department = @Dept
                    WHERE UserId = @Id AND IsDeleted = 0",
                    DbHelper.P("@Name", fullName),
                    DbHelper.P("@Email", email),
                    DbHelper.P("@Phone", phone),
                    DbHelper.P("@Dept", department),
                    DbHelper.P("@Id", userId));
            }
        }

        public UserLoginResult? GetProfile(string email)
        {
            var dt = _db.ExecuteQuery("sp_GetUserAuthData", DbHelper.P("@Email", email));
            if (dt.Rows.Count == 0) return null;

            var row = dt.Rows[0];
            var result = new UserLoginResult
            {
                UserId = Convert.ToInt32(row["UserId"]),
                CompanyId = Convert.ToInt32(row["CompanyId"]),
                RoleName = row["RoleName"].ToString() ?? "Staff",
                FullName = row["FullName"].ToString() ?? "",
                Email = row["Email"].ToString() ?? "",
                Company = row["CompanyName"].ToString() ?? "",
                Phone = row.Table.Columns.Contains("Phone") && row["Phone"] != DBNull.Value ? row["Phone"].ToString() : "",
                Department = row.Table.Columns.Contains("Department") && row["Department"] != DBNull.Value ? row["Department"].ToString() : ""
            };

            if (row["ProfileImageData"] != DBNull.Value)
            {
                result.ProfileImageData = (byte[])row["ProfileImageData"];
                result.ProfileImageMimeType = row["ProfileImageMimeType"].ToString();
            }

            return result;
        }

        public bool SaveResetCode(string email, string code)
        {
            var dt = _db.ExecuteQuery("sp_UpdateResetCode",
                DbHelper.P("@Email", email),
                DbHelper.P("@Code", code));

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["RowsAffected"]) > 0;
            }
            return false;
        }

        public bool VerifyResetCode(string email, string code)
        {
            var dt = _db.ExecuteQuery("sp_VerifyResetCode",
                DbHelper.P("@Email", email),
                DbHelper.P("@Code", code));

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["IsValid"]) == 1;
            }
            return false;
        }

        public void ResetPassword(string email, string passwordHash)
        {
            _db.ExecuteNonQuery("sp_CompletePasswordReset",
                DbHelper.P("@Email", email),
                DbHelper.P("@NewPasswordHash", passwordHash));
        }
    }
}