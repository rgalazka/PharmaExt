namespace PharmaExt.App.Models;

public enum LabelType
{
    Zewnetrznie,
    Wewnetrznie
}

public enum LabelSize
{
    Mala,
    Duza
}

public enum FormStatus
{
    Imported,
    InProgress,
    Completed,
    PdfGenerated
}

public enum GeneratedDocumentType
{
    ProtocolWithA4Label,
    ExternalBrotherLabels,
    InternalBrotherLabels
}
