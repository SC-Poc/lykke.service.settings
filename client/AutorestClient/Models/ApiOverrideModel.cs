// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.Service.SettingsServiceV2.Client.AutorestClient.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class ApiOverrideModel
    {
        /// <summary>
        /// Initializes a new instance of the ApiOverrideModel class.
        /// </summary>
        public ApiOverrideModel()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ApiOverrideModel class.
        /// </summary>
        /// <param name="status">Possible values include: 'Ok',
        /// 'JsonFormarIncorrrect', 'OutOfDate', 'InternalError', 'NotFound',
        /// 'HasDuplicated', 'AlreadyExists', 'InvalidInput'</param>
        public ApiOverrideModel(UpdateSettingsStatus status, IList<IKeyValueEntity> keyValues = default(IList<IKeyValueEntity>), IList<string> duplicatedKeys = default(IList<string>))
        {
            Status = status;
            KeyValues = keyValues;
            DuplicatedKeys = duplicatedKeys;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets possible values include: 'Ok', 'JsonFormarIncorrrect',
        /// 'OutOfDate', 'InternalError', 'NotFound', 'HasDuplicated',
        /// 'AlreadyExists', 'InvalidInput'
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public UpdateSettingsStatus Status { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "keyValues")]
        public IList<IKeyValueEntity> KeyValues { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "duplicatedKeys")]
        public IList<string> DuplicatedKeys { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="Microsoft.Rest.ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
        }
    }
}
