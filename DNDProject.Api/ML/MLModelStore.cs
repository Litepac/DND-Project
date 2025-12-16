using Microsoft.ML;

namespace DNDProject.Api.ML;

public sealed class MLModelStore
{
    private readonly object _gate = new();

    private ITransformer? _model;
    private Dictionary<string, double>? _residualStdBySk;
    private DateTime _trainedFrom;
    private DateTime _trainedTo;
    private DateTime _trainedAtUtc;

    public bool HasModel
    {
        get { lock (_gate) return _model is not null && _residualStdBySk is not null; }
    }

    public bool TryGet(out ITransformer model, out Dictionary<string, double> residualStdBySk,
                       out DateTime trainedFrom, out DateTime trainedTo, out DateTime trainedAtUtc)
    {
        lock (_gate)
        {
            if (_model is null || _residualStdBySk is null)
            {
                model = default!;
                residualStdBySk = default!;
                trainedFrom = default;
                trainedTo = default;
                trainedAtUtc = default;
                return false;
            }

            model = _model;
            residualStdBySk = _residualStdBySk;
            trainedFrom = _trainedFrom;
            trainedTo = _trainedTo;
            trainedAtUtc = _trainedAtUtc;
            return true;
        }
    }

    public void Set(ITransformer model, Dictionary<string, double> residualStdBySk, DateTime from, DateTime to)
    {
        lock (_gate)
        {
            _model = model;
            _residualStdBySk = residualStdBySk;
            _trainedFrom = from.Date;
            _trainedTo = to.Date;
            _trainedAtUtc = DateTime.UtcNow;
        }
    }
}
