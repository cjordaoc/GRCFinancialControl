using System;

namespace GRCFinancialControl.Core.Exceptions;

/// <summary>
/// Base exception for all domain-specific errors in the GRC Financial Control application.
/// </summary>
public abstract class ApplicationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationException"/> class.
    /// </summary>
    protected ApplicationException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message that explains the exception.</param>
    protected ApplicationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the exception.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    protected ApplicationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when an import operation encounters an error.
/// </summary>
public sealed class ImportException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportException"/> class.
    /// </summary>
    public ImportException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message that explains the import failure.</param>
    public ImportException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the import failure.</param>
    /// <param name="innerException">The exception that caused this import error.</param>
    public ImportException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when an allocation operation encounters an error (e.g., locked fiscal year, invalid budget).
/// </summary>
public sealed class AllocationException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationException"/> class.
    /// </summary>
    public AllocationException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message that explains the allocation failure.</param>
    public AllocationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the allocation failure.</param>
    /// <param name="innerException">The exception that caused this allocation error.</param>
    public AllocationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when input validation fails.
/// </summary>
public sealed class ValidationException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    public ValidationException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
    public ValidationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
    /// <param name="innerException">The exception that caused this validation error.</param>
    public ValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when an engagement cannot be mutated due to its state (locked, closed, manual-only).
/// </summary>
public sealed class EngagementMutationException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EngagementMutationException"/> class.
    /// </summary>
    public EngagementMutationException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EngagementMutationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message describing why the engagement cannot be modified.</param>
    public EngagementMutationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EngagementMutationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing why the engagement cannot be modified.</param>
    /// <param name="innerException">The exception that caused this mutation error.</param>
    public EngagementMutationException(string message, Exception innerException)
        : base(message, innerException) { }
}
