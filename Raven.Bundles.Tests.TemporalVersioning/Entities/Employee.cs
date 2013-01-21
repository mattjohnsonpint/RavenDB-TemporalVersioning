using System;

namespace Raven.Bundles.Tests.TemporalVersioning.Entities
{
    // Employee is a temporal entity.
    // Any change to the employee is tracked.

    public class Employee 
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal PayRate { get; set; }
        public string ManagerId { get; set; }
        public string DepartmentId { get; set; }
        public DateTime HireDate { get; set; }
    }
}
