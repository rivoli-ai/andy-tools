using Andy.Tools.Core;
using Andy.Data;
using Andy.Data.Backend;
using Andy.Data.Operations;

namespace Andy.Tools.Data;

// Thin Andy ITool adapters over the framework-independent Andy.Data operations. All behavior lives
// in Andy.Data; each tool only wires constructor dependencies and (where it differs) declares the
// capability flags it needs. The parameterless constructor is used by the tool registry to read
// metadata; the dependency constructor is used at execution time.

public sealed class LoadCsvTool : DataFrameToolAdapter
{
    public LoadCsvTool() : base(new LoadCsvOperation(null!, null!, null)) { }

    public LoadCsvTool(IDuckDbBackend backend, IDatasetCatalog catalog, IPathPolicy? pathPolicy = null)
        : base(new LoadCsvOperation(backend, catalog, pathPolicy)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.FileSystemRead;
}

public sealed class LoadJsonTool : DataFrameToolAdapter
{
    public LoadJsonTool() : base(new LoadJsonOperation(null!, null!, null)) { }

    public LoadJsonTool(IDuckDbBackend backend, IDatasetCatalog catalog, IPathPolicy? pathPolicy = null)
        : base(new LoadJsonOperation(backend, catalog, pathPolicy)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.FileSystemRead;
}

public sealed class LoadParquetTool : DataFrameToolAdapter
{
    public LoadParquetTool() : base(new LoadParquetOperation(null!, null!, null)) { }

    public LoadParquetTool(IDuckDbBackend backend, IDatasetCatalog catalog, IPathPolicy? pathPolicy = null)
        : base(new LoadParquetOperation(backend, catalog, pathPolicy)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.FileSystemRead;
}

public sealed class LoadDeltaTool : DataFrameToolAdapter
{
    public LoadDeltaTool() : base(new LoadDeltaOperation(null!, null!, null)) { }

    public LoadDeltaTool(IDuckDbBackend backend, IDatasetCatalog catalog, IPathPolicy? pathPolicy = null)
        : base(new LoadDeltaOperation(backend, catalog, pathPolicy)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.FileSystemRead;
}

public sealed class ExportTool : DataFrameToolAdapter
{
    public ExportTool() : base(new ExportOperation(null!, null!, null)) { }

    public ExportTool(IDuckDbBackend backend, IDatasetCatalog catalog, IPathPolicy? pathPolicy = null)
        : base(new ExportOperation(backend, catalog, pathPolicy)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite;
}

public sealed class SchemaTool : DataFrameToolAdapter
{
    public SchemaTool() : base(new SchemaOperation(null!)) { }

    public SchemaTool(IDatasetCatalog catalog) : base(new SchemaOperation(catalog)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.None;
}

public sealed class ListTool : DataFrameToolAdapter
{
    public ListTool() : base(new ListOperation(null!)) { }

    public ListTool(IDatasetCatalog catalog) : base(new ListOperation(catalog)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.None;
}

public sealed class ProfileTool : DataFrameToolAdapter
{
    public ProfileTool() : base(new ProfileOperation(null!, null!)) { }

    public ProfileTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new ProfileOperation(backend, catalog)) { }
}

public sealed class PreviewTool : DataFrameToolAdapter
{
    public PreviewTool() : base(new PreviewOperation(null!, null!)) { }

    public PreviewTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new PreviewOperation(backend, catalog)) { }
}

public sealed class ValueCountsTool : DataFrameToolAdapter
{
    public ValueCountsTool() : base(new ValueCountsOperation(null!, null!)) { }

    public ValueCountsTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new ValueCountsOperation(backend, catalog)) { }
}

public sealed class AssertTool : DataFrameToolAdapter
{
    public AssertTool() : base(new AssertOperation(null!, null!)) { }

    public AssertTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new AssertOperation(backend, catalog)) { }
}

public sealed class SelectTool : DataFrameToolAdapter
{
    public SelectTool() : base(new SelectOperation(null!, null!)) { }

    public SelectTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new SelectOperation(backend, catalog)) { }
}

public sealed class FilterTool : DataFrameToolAdapter
{
    public FilterTool() : base(new FilterOperation(null!, null!)) { }

    public FilterTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new FilterOperation(backend, catalog)) { }
}

public sealed class WithColumnTool : DataFrameToolAdapter
{
    public WithColumnTool() : base(new WithColumnOperation(null!, null!)) { }

    public WithColumnTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new WithColumnOperation(backend, catalog)) { }
}

public sealed class RenameTool : DataFrameToolAdapter
{
    public RenameTool() : base(new RenameOperation(null!, null!)) { }

    public RenameTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new RenameOperation(backend, catalog)) { }
}

public sealed class GroupByTool : DataFrameToolAdapter
{
    public GroupByTool() : base(new GroupByOperation(null!, null!)) { }

    public GroupByTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new GroupByOperation(backend, catalog)) { }
}

public sealed class WindowTool : DataFrameToolAdapter
{
    public WindowTool() : base(new WindowOperation(null!, null!)) { }

    public WindowTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new WindowOperation(backend, catalog)) { }
}

public sealed class PivotTool : DataFrameToolAdapter
{
    public PivotTool() : base(new PivotOperation(null!, null!)) { }

    public PivotTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new PivotOperation(backend, catalog)) { }
}

public sealed class UnpivotTool : DataFrameToolAdapter
{
    public UnpivotTool() : base(new UnpivotOperation(null!, null!)) { }

    public UnpivotTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new UnpivotOperation(backend, catalog)) { }
}

public sealed class UnnestTool : DataFrameToolAdapter
{
    public UnnestTool() : base(new UnnestOperation(null!, null!)) { }

    public UnnestTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new UnnestOperation(backend, catalog)) { }
}

public sealed class JoinTool : DataFrameToolAdapter
{
    public JoinTool() : base(new JoinOperation(null!, null!)) { }

    public JoinTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new JoinOperation(backend, catalog)) { }
}

public sealed class SampleTool : DataFrameToolAdapter
{
    public SampleTool() : base(new SampleOperation(null!, null!)) { }

    public SampleTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new SampleOperation(backend, catalog)) { }
}

public sealed class SortTool : DataFrameToolAdapter
{
    public SortTool() : base(new SortOperation(null!, null!)) { }

    public SortTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new SortOperation(backend, catalog)) { }
}

public sealed class DistinctTool : DataFrameToolAdapter
{
    public DistinctTool() : base(new DistinctOperation(null!, null!)) { }

    public DistinctTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new DistinctOperation(backend, catalog)) { }
}

public sealed class UnionTool : DataFrameToolAdapter
{
    public UnionTool() : base(new UnionOperation(null!, null!)) { }

    public UnionTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new UnionOperation(backend, catalog)) { }
}

public sealed class FillnaTool : DataFrameToolAdapter
{
    public FillnaTool() : base(new FillnaOperation(null!, null!)) { }

    public FillnaTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new FillnaOperation(backend, catalog)) { }
}

public sealed class DropnaTool : DataFrameToolAdapter
{
    public DropnaTool() : base(new DropnaOperation(null!, null!)) { }

    public DropnaTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new DropnaOperation(backend, catalog)) { }
}

public sealed class DropTool : DataFrameToolAdapter
{
    public DropTool() : base(new DropOperation(null!, null!)) { }

    public DropTool(IDuckDbBackend backend, IDatasetCatalog catalog)
        : base(new DropOperation(backend, catalog)) { }
    protected override ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.None;
}
