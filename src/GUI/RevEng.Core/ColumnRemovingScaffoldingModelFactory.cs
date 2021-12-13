﻿#if CORE60
using System.Diagnostics.CodeAnalysis;
#else
using JetBrains.Annotations;
#endif
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using RevEng.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevEng.Core
{
    public class ColumnRemovingScaffoldingModelFactory : RelationalScaffoldingModelFactory
    {
        private readonly List<SerializationTableModel> _tables;
        private readonly List<ColumnTypeMapping> _columnTypeMappings = null;
        private readonly DatabaseType _databaseType;
#if CORE60
        private readonly bool _ignoreManyToMany;

        public ColumnRemovingScaffoldingModelFactory([NotNull] IOperationReporter reporter, [NotNull] ICandidateNamingService candidateNamingService, [NotNull] IPluralizer pluralizer, [NotNull] ICSharpUtilities cSharpUtilities, [NotNull] IScaffoldingTypeMapper scaffoldingTypeMapper, [NotNull] LoggingDefinitions loggingDefinitions, [NotNull] IModelRuntimeInitializer modelRuntimeInitializer, List<SerializationTableModel> tables, List<ColumnTypeMapping> columnTypeMappings, DatabaseType databaseType, bool ignoreManyToMany) :
            base(reporter, candidateNamingService, pluralizer, cSharpUtilities, scaffoldingTypeMapper, loggingDefinitions, modelRuntimeInitializer)
#else
        public ColumnRemovingScaffoldingModelFactory([NotNull] IOperationReporter reporter, [NotNull] ICandidateNamingService candidateNamingService, [NotNull] IPluralizer pluralizer, [NotNull] ICSharpUtilities cSharpUtilities, [NotNull] IScaffoldingTypeMapper scaffoldingTypeMapper, [NotNull] LoggingDefinitions loggingDefinitions, List<SerializationTableModel> tables, List<ColumnTypeMapping> columnTypeMappings, DatabaseType databaseType) :
            base(reporter, candidateNamingService, pluralizer, cSharpUtilities, scaffoldingTypeMapper, loggingDefinitions)
#endif
        {
            _tables = tables;
            _databaseType = databaseType;
#if CORE60
            _ignoreManyToMany = ignoreManyToMany;
#endif
        }

        protected override EntityTypeBuilder VisitTable(ModelBuilder modelBuilder, DatabaseTable table)
        {
            string name;
            if (_databaseType == DatabaseType.SQLServer || _databaseType == DatabaseType.SQLServerDacpac)
            {
                name = $"[{table.Schema}].[{table.Name}]";
            }
            else
            {
                name = string.IsNullOrEmpty(table.Schema)
                    ? table.Name
                    : $"{table.Schema}.{table.Name}";
            }

            var tableDefinition = _tables.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (tableDefinition?.ExcludedColumns != null)
            {
                foreach (var column in tableDefinition?.ExcludedColumns)
                {
                    var columnToRemove = table.Columns.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                    if (columnToRemove != null)
                    {
                        table.Columns.Remove(columnToRemove);
                    }
                }
            }

            return base.VisitTable(modelBuilder, table);
        }

#if CORE60
        protected override ModelBuilder VisitForeignKeys(ModelBuilder modelBuilder, IList<DatabaseForeignKey> foreignKeys)
        {
            if (_ignoreManyToMany)
            {
                foreach (var fk in foreignKeys)
                {
                    VisitForeignKey(modelBuilder, fk);
                }

                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    foreach (var foreignKey in entityType.GetForeignKeys())
                    {
                        AddNavigationProperties(foreignKey);
                    }
                }

                return modelBuilder;
            }

            return base.VisitForeignKeys(modelBuilder, foreignKeys);
        }
#endif

        protected override TypeScaffoldingInfo GetTypeScaffoldingInfo(DatabaseColumn column)
        {
            if (_columnTypeMappings == null || _columnTypeMappings.Count == 0)
                return base.GetTypeScaffoldingInfo(column);

            var mapping = _columnTypeMappings.FirstOrDefault(_ => _.StoreType == column.StoreType);
            if (mapping == null)
                return base.GetTypeScaffoldingInfo(column);

            var clrType = Type.GetType(mapping.ClrType);
            if (clrType == null)
                throw new Exception($"Not able to find ClrType: {mapping.ClrType}");

#if CORE50 || CORE60
            return new TypeScaffoldingInfo(clrType,
                                            mapping.IsInferred,
                                            mapping.ScaffoldUnicode,
                                            mapping.ScaffoldMaxLength,
                                            mapping.ScaffoldFixedLength,
                                            mapping.ScaffoldPrecision,
                                            mapping.ScaffoldScale);
#else
            return new TypeScaffoldingInfo(clrType,
                                            mapping.IsInferred,
                                            mapping.ScaffoldUnicode,
                                            mapping.ScaffoldMaxLength,
                                            mapping.ScaffoldFixedLength);
#endif
        }
    }
}
