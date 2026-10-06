using System;
using System.Collections.Generic;
using System.Linq;

namespace FlexiSpace.Core.Common
{
    // Thrown when a referenced entity (Boardroom, User, Equipment, Catering, etc.)
    // does not exist. Controllers should catch this and return HTTP 404 Not Found.
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }

    // Thrown when a request is well-formed but violates a business rule
    // (e.g. booking a room in the past, attendees over capacity, invalid
    // status transition). Controllers should catch this and return HTTP 400
    // Bad Request. A single call can surface more than one violation at once,
    // so Errors holds every rule that failed rather than just the first one.
    public class BusinessRuleException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public BusinessRuleException(string error) : base(error)
        {
            Errors = new List<string> { error };
        }

        public BusinessRuleException(IEnumerable<string> errors)
            : base(string.Join(" ", errors))
        {
            Errors = errors.ToList();
        }
    }
}
