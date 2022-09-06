using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityServer3.Core.Models
{
    public abstract class AuthenticationContextBase
    {
        /// <summary>
        /// Gets or sets the sign in message.
        /// </summary>
        /// <value>
        /// The sign in message.
        /// </value>
        public SignInMessage SignInMessage { get; set; }

        /// <summary>
        /// Gets or sets the authenticate result.
        /// </summary>
        /// <value>
        /// The authenticate result.
        /// </value>
        public AuthenticateResult AuthenticateResult { get; set; }
    }
}
