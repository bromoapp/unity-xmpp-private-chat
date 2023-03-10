using System;

using Xmpp.Xml.Dom;

namespace Xmpp.protocol.extensions.amp
{
    public class Rule : Element
    {
        public Rule()
        {
            this.TagName = "rule";
            this.Namespace = Uri.AMP;
        }

        public Rule(Condition condition, string val, Action action)
            : this()
        {
            Condition   = condition;
            Val         = val;
            Action      = action;
        }

        /// <summary>
        /// The 'value' attribute defines how the condition is matched. 
        /// This attribute MUST be present, and MUST NOT be an empty string (""). 
        /// The interpretation of this attribute's value is determined by the 'condition' attribute.
        /// </summary>
        public string Val
        {
            get { return GetAttribute("value"); }
            set { SetAttribute("value", value); }
        }

        /// <summary>
        /// The 'action' attribute defines the result for this rule. 
        /// This attribute MUST be present, and MUST be either a value defined in the Defined Actions section, 
        /// or one registered with the XMPP Registrar.
        /// </summary>
        public Action Action
        {
            get
            {
                return (Action) GetAttributeEnum("action", typeof(Action));
            }
            set
            {
                if (value ==Action.Unknown)
                    RemoveAttribute("action");
                else
                    SetAttribute("action", value.ToString());
            }
        }

        /// <summary>
        /// The 'condition' attribute defines the overall condition this rule applies to. 
        /// This attribute MUST be present, and MUST be either a value defined in the Defined Conditions section, 
        /// or one registered with the XMPP Registrar.
        /// </summary>
        public Condition Condition
        {
            get
            {
                switch (GetAttribute("condition"))
                {
                    case "deliver":
                        return Condition.Deliver;                       
                    case "expire-at":
                        return Condition.ExprireAt;                        
                    case "match-resource":
                        return Condition.MatchResource;                      
                    default:
                        return Condition.Unknown;
                }
            }

            set
            {
                switch (value)
                {
                    case Condition.Deliver:
                        SetAttribute("condition", "deliver");
                        break;
                    case Condition.ExprireAt:
                        SetAttribute("condition", "expire-at");
                        break;
                    case Condition.MatchResource:
                        SetAttribute("condition", "match-resource");
                        break;
                }
            }
        }
    }
}