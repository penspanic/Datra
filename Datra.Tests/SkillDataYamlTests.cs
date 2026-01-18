using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Converters;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class SkillDataYamlTests
    {
        private readonly ITestOutputHelper _output;
        private readonly YamlDataSerializer _serializer;

        public SkillDataYamlTests(ITestOutputHelper output)
        {
            _output = output;
            // Register SkillEffect as polymorphic base type
            _serializer = new YamlDataSerializer(new[] { typeof(SkillEffect) });
        }

        [Fact]
        public void LoadSkillsYaml_ShouldDeserializeAllSkills()
        {
            // Arrange
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act
            var skills = DeserializeSkillList(yamlContent);

            // Assert
            Assert.NotEmpty(skills);
            Assert.Equal(7, skills.Count);

            _output.WriteLine($"Loaded {skills.Count} skills:");
            foreach (var skill in skills)
            {
                _output.WriteLine($"  - {skill.Id}: {skill.Type}, {skill.Effects.Count} effects");
            }
        }

        [Fact]
        public void LoadSkillsYaml_Fireball_ShouldHaveDamageEffect()
        {
            // Arrange
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act
            var skills = DeserializeSkillList(yamlContent);
            var fireball = skills.FirstOrDefault(s => s.Id == "skill_fireball");

            // Assert
            Assert.NotNull(fireball);
            Assert.Equal(SkillCategory.Active, fireball.Type);
            Assert.Equal(SkillTargetType.SingleEnemy, fireball.TargetType);
            Assert.Equal(25, fireball.ManaCost);
            Assert.Single(fireball.Effects);

            var damageEffect = fireball.Effects[0] as DamageEffect;
            Assert.NotNull(damageEffect);
            Assert.Equal(50, damageEffect.BaseDamage);
            Assert.Equal(SkillDamageType.Fire, damageEffect.DamageType);
            Assert.Equal(1.2f, damageEffect.DamageMultiplier);

            _output.WriteLine($"Fireball: {fireball.ManaCost} mana, {fireball.Cooldown}s cooldown");
            _output.WriteLine($"  DamageEffect: {damageEffect.BaseDamage} {damageEffect.DamageType} damage");
        }

        [Fact]
        public void LoadSkillsYaml_LightningStorm_ShouldHaveMultipleEffects()
        {
            // Arrange
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act
            var skills = DeserializeSkillList(yamlContent);
            var lightningStorm = skills.FirstOrDefault(s => s.Id == "skill_lightning_storm");

            // Assert
            Assert.NotNull(lightningStorm);
            Assert.Equal(SkillCategory.Ultimate, lightningStorm.Type);
            Assert.Equal(2, lightningStorm.Effects.Count);

            // First effect - DamageEffect
            Assert.IsType<DamageEffect>(lightningStorm.Effects[0]);
            var damage = (DamageEffect)lightningStorm.Effects[0];
            Assert.Equal(SkillDamageType.Lightning, damage.DamageType);

            // Second effect - CrowdControlEffect
            Assert.IsType<CrowdControlEffect>(lightningStorm.Effects[1]);
            var cc = (CrowdControlEffect)lightningStorm.Effects[1];
            Assert.Equal(CrowdControlType.Stun, cc.ControlType);
            Assert.Equal(0.3f, cc.Chance);

            _output.WriteLine($"Lightning Storm effects:");
            _output.WriteLine($"  1. DamageEffect: {damage.BaseDamage} {damage.DamageType}");
            _output.WriteLine($"  2. CrowdControlEffect: {cc.ControlType} ({cc.Chance * 100}% chance)");
        }

        [Fact]
        public void LoadSkillsYaml_FrostNova_ShouldHaveThreeEffects()
        {
            // Arrange
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act
            var skills = DeserializeSkillList(yamlContent);
            var frostNova = skills.FirstOrDefault(s => s.Id == "skill_frost_nova");

            // Assert
            Assert.NotNull(frostNova);
            Assert.Equal(3, frostNova.Effects.Count);

            Assert.IsType<DamageEffect>(frostNova.Effects[0]);
            Assert.IsType<CrowdControlEffect>(frostNova.Effects[1]);
            Assert.IsType<BuffEffect>(frostNova.Effects[2]);

            var debuff = (BuffEffect)frostNova.Effects[2];
            Assert.True(debuff.IsDebuff);
            Assert.Equal(SkillStatType.Defense, debuff.AffectedStat);
            Assert.Equal(-0.2f, debuff.StatModifier);

            _output.WriteLine($"Frost Nova has {frostNova.Effects.Count} effects:");
            foreach (var effect in frostNova.Effects)
            {
                _output.WriteLine($"  - {effect.GetType().Name}: {effect.Description}");
            }
        }

        [Fact]
        public void LoadSkillsYaml_AllEffectTypes_ShouldBePresent()
        {
            // Arrange
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act
            var skills = DeserializeSkillList(yamlContent);
            var allEffects = skills.SelectMany(s => s.Effects).ToList();

            // Assert - all 5 effect types should be present
            Assert.Contains(allEffects, e => e is DamageEffect);
            Assert.Contains(allEffects, e => e is HealEffect);
            Assert.Contains(allEffects, e => e is BuffEffect);
            Assert.Contains(allEffects, e => e is SummonEffect);
            Assert.Contains(allEffects, e => e is CrowdControlEffect);

            _output.WriteLine($"Effect type distribution:");
            _output.WriteLine($"  DamageEffect: {allEffects.Count(e => e is DamageEffect)}");
            _output.WriteLine($"  HealEffect: {allEffects.Count(e => e is HealEffect)}");
            _output.WriteLine($"  BuffEffect: {allEffects.Count(e => e is BuffEffect)}");
            _output.WriteLine($"  SummonEffect: {allEffects.Count(e => e is SummonEffect)}");
            _output.WriteLine($"  CrowdControlEffect: {allEffects.Count(e => e is CrowdControlEffect)}");
        }

        [Fact]
        public void RoundTrip_SkillData_ShouldPreservePolymorphicEffects()
        {
            // Arrange - Load fireball from file
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            var skills = DeserializeSkillList(yamlContent);
            var originalSkill = skills.First(s => s.Id == "skill_fireball");

            // Act - serialize
            var yaml = _serializer.SerializeSingle(originalSkill);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);

            // Act - deserialize
            var deserialized = _serializer.DeserializeSingle<SkillData>(yaml);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(originalSkill.Id, deserialized.Id);
            Assert.Equal(originalSkill.Type, deserialized.Type);
            Assert.Equal(originalSkill.ManaCost, deserialized.ManaCost);
            Assert.Equal(originalSkill.Effects.Count, deserialized.Effects.Count);

            // Check polymorphic types preserved
            Assert.IsType<DamageEffect>(deserialized.Effects[0]);

            var damage = (DamageEffect)deserialized.Effects[0];
            Assert.Equal(50, damage.BaseDamage);
            Assert.Equal(SkillDamageType.Fire, damage.DamageType);

            _output.WriteLine("Round-trip successful! Polymorphic types preserved.");
        }

        [Fact]
        public void RoundTrip_FrostNova_ShouldPreserveMultipleEffects()
        {
            // Arrange - Load frost nova which has 3 different effect types
            var dataPath = TestDataHelper.FindDataPath();
            var yamlPath = Path.Combine(dataPath, "Skills.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            var skills = DeserializeSkillList(yamlContent);
            var originalSkill = skills.First(s => s.Id == "skill_frost_nova");

            // Act - serialize
            var yaml = _serializer.SerializeSingle(originalSkill);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);

            // Act - deserialize
            var deserialized = _serializer.DeserializeSingle<SkillData>(yaml);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(3, deserialized.Effects.Count);

            // Check polymorphic types preserved
            Assert.IsType<DamageEffect>(deserialized.Effects[0]);
            Assert.IsType<CrowdControlEffect>(deserialized.Effects[1]);
            Assert.IsType<BuffEffect>(deserialized.Effects[2]);

            var buff = (BuffEffect)deserialized.Effects[2];
            Assert.True(buff.IsDebuff);
            Assert.Equal(-0.2f, buff.StatModifier);

            _output.WriteLine("Round-trip successful! All 3 effect types preserved.");
        }

        [Fact]
        public async Task LoadSkillsYaml_WithDataContext_ShouldWork()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();

            // Act
            await context.LoadAllAsync();

            // Assert - Skills should be loaded from YAML
            Assert.NotEmpty(context.Skill.LoadedItems);
            Assert.Equal(7, context.Skill.Count);

            // Verify polymorphic effects loaded correctly
            var fireball = context.Skill.LoadedItems["skill_fireball"];
            Assert.NotNull(fireball);
            Assert.Single(fireball.Effects);
            Assert.IsType<DamageEffect>(fireball.Effects[0]);

            _output.WriteLine($"Loaded {context.Skill.Count} skills via DataContext");
            foreach (var kvp in context.Skill.LoadedItems)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value.Effects.Count} effects");
            }
        }

        /// <summary>
        /// Helper method to deserialize skill list from YAML.
        /// </summary>
        private List<SkillData> DeserializeSkillList(string yamlContent)
        {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(new PolymorphicYamlTypeConverter(
                    new PortableTypeResolver(),
                    new System.Collections.Generic.HashSet<System.Type> { typeof(SkillEffect) }))
                .Build();

            using var reader = new StringReader(yamlContent);
            return deserializer.Deserialize<List<SkillData>>(reader)
                   ?? new List<SkillData>();
        }
    }
}
