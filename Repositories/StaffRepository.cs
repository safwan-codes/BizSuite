using System;
using System.Collections.Generic;
using System.Data;
using BizSuite.Data;
using BizSuite.Models.Staff;

namespace BizSuite.Repositories
{
    public class StaffRepository : IStaffRepository
    {
        private readonly DbHelper _db;

        public StaffRepository(DbHelper db)
        {
            _db = db;
        }

        public IEnumerable<StaffMember> GetAll(int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetUsers", DbHelper.P("@CompanyId", companyId));
            var list = new List<StaffMember>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new StaffMember
                {
                    StaffId = Convert.ToInt32(row["UserId"]),
                    CompanyId = companyId,
                    FullName = row["FullName"].ToString() ?? "",
                    Email = row["Email"].ToString() ?? "",
                    Phone = row["Phone"] != DBNull.Value ? row["Phone"].ToString() : "", 
                    Role = row["RoleName"].ToString() ?? "Staff",
                    Department = row["Department"] != DBNull.Value ? row["Department"].ToString() : "", 
                    JoinDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]).ToString("dd MMM yyyy") : "",
                    IsActive = true 
                });
            }
            return list;
        }

        public StaffMember? GetById(int staffId, int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetUsers", DbHelper.P("@CompanyId", companyId));
            foreach (DataRow row in dt.Rows)
            {
                if (Convert.ToInt32(row["UserId"]) == staffId)
                {
                    return new StaffMember
                    {
                        StaffId = Convert.ToInt32(row["UserId"]),
                        CompanyId = companyId,
                        FullName = row["FullName"].ToString() ?? "",
                        Email = row["Email"].ToString() ?? "",
                        Phone = row["Phone"] != DBNull.Value ? row["Phone"].ToString() : "",
                        Role = row["RoleName"].ToString() ?? "Staff",
                        Department = row["Department"] != DBNull.Value ? row["Department"].ToString() : "",
                        JoinDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]).ToString("dd MMM yyyy") : "",
                        IsActive = true
                    };
                }
            }
            return null;
        }

        public void Insert(StaffMember staff)
        {
            int roleId = staff.Role == "Admin" ? 1 : (staff.Role == "Manager" ? 2 : 3);
            _db.ExecuteNonQuery("sp_UpsertUser",
                DbHelper.P("@CompanyId", staff.CompanyId),
                DbHelper.P("@UserId", 0),
                DbHelper.P("@RoleId", roleId),
                DbHelper.P("@FullName", staff.FullName),
                DbHelper.P("@Email", staff.Email),
                DbHelper.P("@Phone", staff.Phone),
                DbHelper.P("@Department", staff.Department),
                DbHelper.P("@PasswordHash", BCrypt.Net.BCrypt.HashPassword("Bizsuite@123")), 
                DbHelper.P("@CreatedBy", staff.CompanyId) 
            );
        }

        public void Update(StaffMember staff)
        {
            int roleId = staff.Role == "Admin" ? 1 : (staff.Role == "Manager" ? 2 : 3);
            _db.ExecuteNonQuery("sp_UpsertUser",
                DbHelper.P("@CompanyId", staff.CompanyId),
                DbHelper.P("@UserId", staff.StaffId),
                DbHelper.P("@RoleId", roleId),
                DbHelper.P("@FullName", staff.FullName),
                DbHelper.P("@Email", staff.Email),
                DbHelper.P("@Phone", staff.Phone),
                DbHelper.P("@Department", staff.Department),
                DbHelper.P("@PasswordHash", null), 
                DbHelper.P("@CreatedBy", staff.CompanyId)
            );
        }

        public void Delete(int staffId, int companyId)
        {
            _db.ExecuteNonQuery("sp_DeleteUser",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@UserId", staffId)
            );
        }
    }
}
