using Microsoft.Kiota.Abstractions.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Kiota.Builder.SearchProviders.GitHub.GitHubClient.Repos.Item.Item.Contents.Item {
    public class WithPathPutRequestBody : IAdditionalDataHolder, IParsable {
        /// <summary>Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.</summary>
        public IDictionary<string, object> AdditionalData { get; set; }
        /// <summary>The author of the file. Default: The `committer` or the authenticated user if you omit `committer`.</summary>
        public WithPathPutRequestBody_author Author { get; set; }
        /// <summary>The branch name. Default: the repository’s default branch (usually `master`)</summary>
        public string Branch { get; set; }
        /// <summary>The person that committed the file. Default: the authenticated user.</summary>
        public WithPathPutRequestBody_committer Committer { get; set; }
        /// <summary>The new file content, using Base64 encoding.</summary>
        public string Content { get; set; }
        /// <summary>The commit message.</summary>
        public string Message { get; set; }
        /// <summary>**Required if you are updating a file**. The blob SHA of the file being replaced.</summary>
        public string Sha { get; set; }
        /// <summary>
        /// Instantiates a new WithPathPutRequestBody and sets the default values.
        /// </summary>
        public WithPathPutRequestBody() {
            AdditionalData = new Dictionary<string, object>();
        }
        /// <summary>
        /// Creates a new instance of the appropriate class based on discriminator value
        /// </summary>
        /// <param name="parseNode">The parse node to use to read the discriminator value and create the object</param>
        public static WithPathPutRequestBody CreateFromDiscriminatorValue(IParseNode parseNode) {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new WithPathPutRequestBody();
        }
        /// <summary>
        /// The deserialization information for the current model
        /// </summary>
        public IDictionary<string, Action<IParseNode>> GetFieldDeserializers() {
            return new Dictionary<string, Action<IParseNode>> {
                {"author", n => { Author = n.GetObjectValue<WithPathPutRequestBody_author>(WithPathPutRequestBody_author.CreateFromDiscriminatorValue); } },
                {"branch", n => { Branch = n.GetStringValue(); } },
                {"committer", n => { Committer = n.GetObjectValue<WithPathPutRequestBody_committer>(WithPathPutRequestBody_committer.CreateFromDiscriminatorValue); } },
                {"content", n => { Content = n.GetStringValue(); } },
                {"message", n => { Message = n.GetStringValue(); } },
                {"sha", n => { Sha = n.GetStringValue(); } },
            };
        }
        /// <summary>
        /// Serializes information the current object
        /// </summary>
        /// <param name="writer">Serialization writer to use to serialize this model</param>
        public void Serialize(ISerializationWriter writer) {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<WithPathPutRequestBody_author>("author", Author);
            writer.WriteStringValue("branch", Branch);
            writer.WriteObjectValue<WithPathPutRequestBody_committer>("committer", Committer);
            writer.WriteStringValue("content", Content);
            writer.WriteStringValue("message", Message);
            writer.WriteStringValue("sha", Sha);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
