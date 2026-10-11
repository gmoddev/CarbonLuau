namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Data only: reproduced processed-body candidates for Rust build 25653776.
        // Semantic/EH/locals/slot parity proved on both pinned backing binaries.
        // New B2 bodies are NOT live qualified until the main fixture matches.
        // 67 = 5 combat Die + 55 combat OnDied + 4 resource OnDied + Instant + 2 resets.
        // BasePlayer.Die is an existing A carrier; BaseVehicleModule.Die is no-op data.
        // token (8 lowercase hex digits, no 0x)|size|WindowsRawSHA256|LinuxRawSHA256|ExpectedEH
        // ExpectedEH: comma-joined TryOffset:TryLength:HandlerOffset:HandlerLength:Flags.
        // Empty final field means no EH. Flags match Clause.Flags text: Finally.
        // Source: tools/Reproduce-EntityDeathPins.ps1; generated evidence stays in Temp.
        private static readonly string[] EntityDeathPinRows =
        {
            // System.Void AutoTurret::OnDied(HitInfo)
            "0600131d|62|788e22879b399c8dba0a97e3af1b9612727add917b004b2f2aad836f78a5ea9b|788e22879b399c8dba0a97e3af1b9612727add917b004b2f2aad836f78a5ea9b|",
            // System.Void BallistaGun::OnDied(HitInfo)
            "06001399|49|c920d835b5d6f2dca57ca29b2c42b02f0380cb302d73fbfc32d2a1c63eb08b11|c920d835b5d6f2dca57ca29b2c42b02f0380cb302d73fbfc32d2a1c63eb08b11|",
            // System.Void BaseCombatEntity::OnDied(HitInfo)
            "0600149a|91|daa8a0c2911a0709d5adfe726e2ac9823bc826af696c0cfe1239939f6652e095|daa8a0c2911a0709d5adfe726e2ac9823bc826af696c0cfe1239939f6652e095|",
            // System.Void BaseCombatEntity::Die(HitInfo)
            "0600149b|337|ab193eb2cb6a1abe502927eb2ca174993e02674b8883b265a2931e12b661c363|ab193eb2cb6a1abe502927eb2ca174993e02674b8883b265a2931e12b661c363|308:16:324:12:Finally",
            // System.Void BaseCombatEntity::DieInstantly()
            "0600149c|55|b61d5b184ab5e966ebfb19a6ca36f78b8fa837fa79a745f696ec915c2a2b4d30|b61d5b184ab5e966ebfb19a6ca36f78b8fa837fa79a745f696ec915c2a2b4d30|",
            // System.Void BaseMountable::OnDied(HitInfo)
            "060017db|14|be0a5d5e8715256a97901dc8936e3b9638a16584684e2d6e95de2ad25b25894a|be0a5d5e8715256a97901dc8936e3b9638a16584684e2d6e95de2ad25b25894a|",
            // System.Void BaseNpc::OnDied(HitInfo)
            "06001875|81|1a21a8238fd62481eb3b75104da8a93b057522a003e7c374e6838860d1543723|1a21a8238fd62481eb3b75104da8a93b057522a003e7c374e6838860d1543723|",
            // System.Void BasePlayer::OnDied(HitInfo)
            "06001abd|2861|617412ad28c15aedd49143a2057e1b4c9c72ce835c97e12b1bc864a83576e9c7|2462879e57b67cbac0ea8f0e079c353cca437458f422554cf1de51d12ec40b1b|62:40:102:14:Finally,522:101:623:14:Finally,1062:128:1190:14:Finally,2625:18:2643:12:Finally",
            // System.Void BasePlayer::Die(HitInfo)
            "06001aea|210|3e6d22f6ace067b6b16a8cc209946f91d39c605bd6c43e5aa61b190e5662b731|3e6d22f6ace067b6b16a8cc209946f91d39c605bd6c43e5aa61b190e5662b731|12:187:199:10:Finally",
            // System.Void BaseSiegeWeapon::OnDied(HitInfo)
            "06001d88|41|a1ded94538af71e91c47966af13f2c5ee1731b895d747653e211196bd8044c28|a1ded94538af71e91c47966af13f2c5ee1731b895d747653e211196bd8044c28|",
            // System.Void BaseSubmarine::OnDied(HitInfo)
            "06001daa|127|99e83ed25cb07f28d37a0efe32b9503fd4c65927271e029faa339bbb975c016e|99e83ed25cb07f28d37a0efe32b9503fd4c65927271e029faa339bbb975c016e|34:71:105:14:Finally",
            // System.Void BaseVehicle::OnDied(HitInfo)
            "06001def|25|47ff9fc314fdc7c717e8f845c1f9432b18d19619c9d88fd918981431ac88066c|47ff9fc314fdc7c717e8f845c1f9432b18d19619c9d88fd918981431ac88066c|",
            // System.Void BaseVehicleModule::Die(HitInfo)
            "06001e7e|1|684888c0ebb17f374298b65ee2807526c066094c701bcc7ebbe1c1095f494fc1|684888c0ebb17f374298b65ee2807526c066094c701bcc7ebbe1c1095f494fc1|",
            // System.Void BoatBuildingStation::OnDied(HitInfo)
            "06001fdc|45|de08f6608fec135f9d53a7ba7829617bde54e5fc696c359c1c3c32c76937fac1|de08f6608fec135f9d53a7ba7829617bde54e5fc696c359c1c3c32c76937fac1|",
            // System.Void BradleyAPC::OnDied(HitInfo)
            "0600206f|991|cbe4b2fb019f03fd5a98fb89cd98878eccd78815406071cec9f7e05f8d6abf1b|cbe4b2fb019f03fd5a98fb89cd98878eccd78815406071cec9f7e05f8d6abf1b|342:37:379:14:Finally,860:37:897:14:Finally",
            // System.Void BuildingPrivlidge::OnDied(HitInfo)
            "060020fa|141|579c53c3cee87aefa1bf9507eaed42e8aa5390563e2995c51e31a7fa71b91fd3|579c53c3cee87aefa1bf9507eaed42e8aa5390563e2995c51e31a7fa71b91fd3|67:35:102:10:Finally",
            // System.Void BuildingPrivlidge::Die(HitInfo)
            "060020fb|46|e0f3ef5c60c3f19d79b86b99c335ef23dcbe0a7c566aedccc7977eb38bbb952b|db5925a22b9dc7bb769fda8a41f564ab6c12f99258e3bd2c2f72f20f296a9677|",
            // System.Void CeilingLight::OnDied(HitInfo)
            "060021fd|14|7e1b8fea699d630502d0070a695bb2bb5560b8e48402cd2585d8320b5034981a|7e1b8fea699d630502d0070a695bb2bb5560b8e48402cd2585d8320b5034981a|",
            // System.Void ContainerIOEntity::OnDied(HitInfo)
            "06002384|15|4b0eda67d745bb4434fc8a803ff6be76e94709ed8b1d3f897ab4f165261b7682|4b0eda67d745bb4434fc8a803ff6be76e94709ed8b1d3f897ab4f165261b7682|",
            // System.Void DecayEntity::OnDied(HitInfo)
            "0600241a|121|64f8f30744b4b5396fe4d687ff27f02412c7dae5de9e805c9b65c5e0f3f22f4e|64f8f30744b4b5396fe4d687ff27f02412c7dae5de9e805c9b65c5e0f3f22f4e|",
            // System.Void Drone::OnDied(HitInfo)
            "06002623|44|eb4843c88b9898764ac95cb03e87631735390e271d1adb05a3fa796aa131f970|eb4843c88b9898764ac95cb03e87631735390e271d1adb05a3fa796aa131f970|",
            // System.Void EaselDeployable::OnDied(HitInfo)
            "06002670|113|dccf0f2ca84703d058b3c11472e5538ddc793573ceabc09b22f1a3c52e4dc32b|dccf0f2ca84703d058b3c11472e5538ddc793573ceabc09b22f1a3c52e4dc32b|",
            // System.Void FarmableAnimal::OnDied(HitInfo)
            "060026ec|75|f5c1b97fcc5e946e85baa6c05ed381df3f99a2a2869f8130060bc92d5d56f037|f5c1b97fcc5e946e85baa6c05ed381df3f99a2a2869f8130060bc92d5d56f037|",
            // System.Void IndustrialCrafter::OnDied(HitInfo)
            "0600294d|26|7035aad513f1d61a30e0d1d47a97e8599e4b7a81e2e5270b901da379403321b8|7035aad513f1d61a30e0d1d47a97e8599e4b7a81e2e5270b901da379403321b8|",
            // System.Void Landmine::OnDied(HitInfo)
            "06002ab6|35|de0d20a88d80606ce3742e3174051dd247ab7787432c6a67ddd5d5004c01d79d|de0d20a88d80606ce3742e3174051dd247ab7787432c6a67ddd5d5004c01d79d|",
            // System.Void MagnetCrane::OnDied(HitInfo)
            "06002b76|91|f87d889b7a7f4dcc3a5ada0745ba3f84be3c224fdca76e7e1a5cbd7467ff1b00|f87d889b7a7f4dcc3a5ada0745ba3f84be3c224fdca76e7e1a5cbd7467ff1b00|",
            // System.Void ModularCar::OnDied(HitInfo)
            "06002c8b|139|48edea52285b4c3b46a10ed490e2fee533b0ad07f6cfd02f6f50a28b96a76eae|2299fb966b46dae740fce10c3accc38bdabfc277010648e0af89542ddfc6148f|18:31:49:14:Finally",
            // System.Void MonumentBlocker::OnDied(HitInfo)
            "06002d19|16|42b436d01f721de0470503293cbb9b5b84d9153f359def25f67509c421972672|42b436d01f721de0470503293cbb9b5b84d9153f359def25f67509c421972672|",
            // System.Void MotorRowboat::OnDied(HitInfo)
            "06002d72|90|9ea2702ef5c6b178dbdcdd2a9716e4ed82f4491962e2f13cf04c4821112d8051|9ea2702ef5c6b178dbdcdd2a9716e4ed82f4491962e2f13cf04c4821112d8051|17:16:33:14:Finally",
            // System.Void NPCShopKeeper::OnDied(HitInfo)
            "06002de7|66|4a72726ee63a30c69fba18d02fe2ce03f1466521b2bfecbe71911a40979b0d8a|4a72726ee63a30c69fba18d02fe2ce03f1466521b2bfecbe71911a40979b0d8a|",
            // System.Void OreResourceEntity::OnDied(HitInfo)
            "06002e14|14|9d64983ca8672793042b5759b48978509eed1a9c2768d18f9dc44ed91bd86990|9d64983ca8672793042b5759b48978509eed1a9c2768d18f9dc44ed91bd86990|",
            // System.Void PatrolHelicopter::OnDied(HitInfo)
            "06002e9e|964|30c4bf35cc702cf8d3f68d0fe52ec895cab5b8014906bb657b533ac99a744d27|30c4bf35cc702cf8d3f68d0fe52ec895cab5b8014906bb657b533ac99a744d27|387:37:424:14:Finally,886:37:923:14:Finally",
            // System.Void PlayerHelicopter::OnDied(HitInfo)
            "06002f66|151|4674dc07d0c590734b8ae30e1add249d7f7a2f9785f562cec0e4fd117053e6d2|4674dc07d0c590734b8ae30e1add249d7f7a2f9785f562cec0e4fd117053e6d2|12:117:129:14:Finally",
            // System.Void RidableHorse::OnDied(HitInfo)
            "06003352|112|9868aa8601a7d428b422cf51f78b794b0476a57bb81b6fc776eeec48dbe533d7|9868aa8601a7d428b422cf51f78b794b0476a57bb81b6fc776eeec48dbe533d7|",
            // System.Void SamSite::SelfHeal()
            "060033cb|58|a55726317b6e258d79b562b19a5cb34b9da62e9355534f68c3f517906038e7f0|a55726317b6e258d79b562b19a5cb34b9da62e9355534f68c3f517906038e7f0|27:16:43:14:Finally",
            // System.Void SamSite::Die(HitInfo)
            "060033cc|167|1c847174188fe1c6ecae9a80106e613a6a1b2fd07d33855e4613fad42f96b794|1c847174188fe1c6ecae9a80106e613a6a1b2fd07d33855e4613fad42f96b794|128:16:144:14:Finally",
            // System.Void SearchLight::OnDied(HitInfo)
            "0600344e|42|0cd3cab65389efe62e87b5feee6eeb71fffdfccf153fed45306317b6afacb32c|0cd3cab65389efe62e87b5feee6eeb71fffdfccf153fed45306317b6afacb32c|8:12:20:14:Finally",
            // System.Void SiegeTower::OnDied(HitInfo)
            "060034aa|85|2cd7badd71a707a0572225f1dbb87a81bd87635bfc4d67e4b96803d03706cdf4|2cd7badd71a707a0572225f1dbb87a81bd87635bfc4d67e4b96803d03706cdf4|27:36:63:14:Finally",
            // System.Void SimpleBuildingBlock::OnDied(HitInfo)
            "0600350a|94|a96f310d9540d63890a624f5bb26f6eda22e2fd08d9befb60c8a2007b83e15e2|a96f310d9540d63890a624f5bb26f6eda22e2fd08d9befb60c8a2007b83e15e2|52:17:69:14:Finally",
            // System.Void SnakeHazard::OnDied(HitInfo)
            "060035dd|75|6a1a06ec61ed1d2eb7a8973e9a6dcffa7bb75c72a803937fbc9493202a1ecffa|6a1a06ec61ed1d2eb7a8973e9a6dcffa7bb75c72a803937fbc9493202a1ecffa|",
            // System.Void StorageContainer::OnDied(HitInfo)
            "060036fe|26|d8d384ee14fc2732ccd9ab28469b1b1e0497e2029ac151b8299c1f6419f9c2b1|d8d384ee14fc2732ccd9ab28469b1b1e0497e2029ac151b8299c1f6419f9c2b1|",
            // System.Void TinCanAlarm::OnDied(HitInfo)
            "060037c1|14|16a1e58f259c272e97e2517780cefab5974d5c00dce22270d65478db7bdb9042|16a1e58f259c272e97e2517780cefab5974d5c00dce22270d65478db7bdb9042|",
            // System.Void TrainCar::OnDied(HitInfo)
            "060037fe|211|e62ae20e3a8b8aefc72d6ad3c832b1272a0763b44e3ef6e7c8b8a095b4bb0495|d758ca0c8cd43d9b5b9dcc2d09a60eaf660f17307a44cfe30a75b817615dd8e6|",
            // System.Void TrainCar::DoRepair(BasePlayer)
            "06003801|54|6124c8e2f625c965bdfb7a8ef5791b39cea03d0e8f5e883fa1393d81a05a7d81|6124c8e2f625c965bdfb7a8ef5791b39cea03d0e8f5e883fa1393d81a05a7d81|",
            // System.Void TreeEntity::OnDied(HitInfo)
            "060038fb|327|c282002cbd6fc96cb6cd7ff3e02d53863930a4181e4cf9630811fe23670ab870|c282002cbd6fc96cb6cd7ff3e02d53863930a4181e4cf9630811fe23670ab870|155:39:194:14:Finally,216:25:241:14:Finally,142:168:310:10:Finally",
            // System.Void WildlifeHazard::OnDied(HitInfo)
            "06003b1c|137|c6586365714cf098204563ecc7a7a49c5a94b9c0e584b96d9e837d3d1879ed57|c6586365714cf098204563ecc7a7a49c5a94b9c0e584b96d9e837d3d1879ed57|",
            // System.Void Workbench::OnDied(HitInfo)
            "06003b86|26|eff213f6a689e13642d2142071bea94cffde331d12c1da2ae741500c4e4def02|eff213f6a689e13642d2142071bea94cffde331d12c1da2ae741500c4e4def02|",
            // System.Void LegacyShelter::OnDied(HitInfo)
            "06003be7|39|3c62179529bc9ab15eea1f39e86b63dc693f057a26dcdee83e3cfa7334d36011|3c62179529bc9ab15eea1f39e86b63dc693f057a26dcdee83e3cfa7334d36011|",
            // System.Void LegacyShelterDoor::OnDied(HitInfo)
            "06003bf7|47|3f8334347e92e1b78bf7340bce3c59339196300f051e7473120b87a03fe646ec|3f8334347e92e1b78bf7340bce3c59339196300f051e7473120b87a03fe646ec|",
            // System.Void Elevator::OnDied(HitInfo)
            "06003cf7|57|a97c7970f42d7f1b25fbcef971cfdef49b559a6950fa0994ad78c147df2c3fd9|a97c7970f42d7f1b25fbcef971cfdef49b559a6950fa0994ad78c147df2c3fd9|",
            // System.Void ElectricalHeater::OnDied(HitInfo)
            "06003da8|33|521f7b3609c9c385a238cd425d169fb70ab35b06cf5779313dfc505a5c8eac6d|521f7b3609c9c385a238cd425d169fb70ab35b06cf5779313dfc505a5c8eac6d|",
            // System.Void OreHotSpot::OnDied(HitInfo)
            "0600401b|14|cf8e0d6c3501172a50d6467ce359c0ac67a4ec8bef0b89f82f11eb56129f35b3|cf8e0d6c3501172a50d6467ce359c0ac67a4ec8bef0b89f82f11eb56129f35b3|",
            // System.Void Pinata::OnDied(HitInfo)
            "0600403c|149|8f3d57be54f08f87cc2bd6995f3a1602cdfc878fccd8a99a194f57ec4af2b22d|8f3d57be54f08f87cc2bd6995f3a1602cdfc878fccd8a99a194f57ec4af2b22d|",
            // System.Void HexagonTile::OnDied(HitInfo)
            "0600407c|46|1baa7fa34d05108a93528ed6889828bcd8253c5b40ebd3665c90c9bf68c28d48|1baa7fa34d05108a93528ed6889828bcd8253c5b40ebd3665c90c9bf68c28d48|15:16:31:14:Finally",
            // System.Void VehicleVendor::OnDied(HitInfo)
            "0600418e|8|995679c21d923f6447536cf2179349970f18c8d00d2f4bd246810e7334b3fb36|995679c21d923f6447536cf2179349970f18c8d00d2f4bd246810e7334b3fb36|",
            // System.Void NaturalBeehive::OnDied(HitInfo)
            "0600424f|14|551be474f1cb5f3055cf3c47783009a010df712add0178fb877dc587708c5fca|551be474f1cb5f3055cf3c47783009a010df712add0178fb877dc587708c5fca|",
            // System.Void SimpleShark::OnDied(HitInfo)
            "06004419|197|773503265529680b354dbb69795f05b2a236ec39e880da528487b519f259bb14|773503265529680b354dbb69795f05b2a236ec39e880da528487b519f259bb14|",
            // System.Void ScientistNPC::OnDied(HitInfo)
            "06004503|125|7c203995151bff879a3bc49722bc6deb921952c5d9c0831d405f392f589fb864|7c203995151bff879a3bc49722bc6deb921952c5d9c0831d405f392f589fb864|",
            // System.Void BaseAnimalNPC::OnDied(HitInfo)
            "060046be|91|acbf57d346b71c0e17a0bf2c4bc174c11f126df0fd7c98df8a9e3ff5b89cb691|acbf57d346b71c0e17a0bf2c4bc174c11f126df0fd7c98df8a9e3ff5b89cb691|",
            // System.Void PlayerBoat::OnDied(HitInfo)
            "06005252|234|f859e2d2f43eac96d2430f05e7a7bcab780d493eaee81c113e3071fc63f1b8d1|f859e2d2f43eac96d2430f05e7a7bcab780d493eaee81c113e3071fc63f1b8d1|24:16:40:14:Finally",
            // System.Void WaterPurifier::OnDied(HitInfo)
            "060054ea|34|248eb27d1a0825805ea1ff5b83c1ecc60f60a7c4068247bc56c4558dd625fb41|248eb27d1a0825805ea1ff5b83c1ecc60f60a7c4068247bc56c4558dd625fb41|",
            // System.Void FlameTurret::OnDied(HitInfo)
            "0600573a|321|7ade83deca141f15a3594e1a93cf2e25d9b804e24921e356871d27c2bc44eae4|7ade83deca141f15a3594e1a93cf2e25d9b804e24921e356871d27c2bc44eae4|",
            // System.Void LootContainer::OnDied(HitInfo)
            "06005773|83|a6851b9116507fcb7e054118e476e0cac8b6a51d39cd10e0824b19e68d0694af|993ae54aa5e6c11444abee185f2fb90bd3c5be17d883b3357d88150bc0ad7329|",
            // System.Void ResourceEntity::OnDied(HitInfo)
            "06005a9e|16|999db78a25a64c512483a4e27f65842c3f679887f83e0851001e2f9e67391175|999db78a25a64c512483a4e27f65842c3f679887f83e0851001e2f9e67391175|",
            // System.Void SiegeTowerDoor::OnDied(HitInfo)
            "06005bda|47|15bc8f9a8c0a0c707d8defa587026e48068f5ac65e25c784c8ebb3a4f564a578|15bc8f9a8c0a0c707d8defa587026e48068f5ac65e25c784c8ebb3a4f564a578|",
            // System.Void BaseHelicopter::OnDied(HitInfo)
            "06005d5e|517|41bb747dd6c5c179254f4331f4c6a18a80524d49847bce7198390fef1f3d886e|41bb747dd6c5c179254f4331f4c6a18a80524d49847bce7198390fef1f3d886e|426:37:463:14:Finally",
            // System.Void CH47HelicopterAIController::OnDied(HitInfo)
            "06005db8|22|000b42c0d359601d69d22882853b0fb80c39690d90d7197fb92c55e71a3c446c|000b42c0d359601d69d22882853b0fb80c39690d90d7197fb92c55e71a3c446c|",
        };
    }
}
