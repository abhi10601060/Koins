package com.example.koins.data.remote.dto

import com.example.koins.domain.model.Coin
import com.google.gson.annotations.SerializedName

data class CoinDto(
    @SerializedName("id") val id : String,
    @SerializedName("name") val name : String,
    @SerializedName("symbol") val symbol : String,
    @SerializedName("rank") val rank : Int,
    @SerializedName("is_new") val isNew : Boolean,
    @SerializedName("is_active") val isActive : Boolean,
    @SerializedName("type") val type : String,
)

fun CoinDto.toCoin() : Coin{
    return Coin(
        id = id,
        isActive = isActive,
        name = name,
        rank = rank,
        symbol = symbol
    )
}