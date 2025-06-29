package com.example.koins.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import androidx.navigation.navigation
import com.example.koins.presentation.coin_list.CoinListScreen
import kotlinx.serialization.Serializable

fun NavGraphBuilder.MainNavGraph(navController: NavController){
    navigation<MainNavigation>(startDestination = CoinListScreen) {
        composable<CoinListScreen> {
            CoinListScreen(navController)
        }
    }
}

@Serializable
object MainNavigation

@Serializable
object CoinListScreen