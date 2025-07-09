package com.example.koins.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import androidx.navigation.navigation
import androidx.navigation.toRoute
import com.example.koins.presentation.coin_details.CoinDetailsScreen
import com.example.koins.presentation.coin_list.CoinListScreen
import com.example.koins.presentation.ssl_pin.component.BookShopScreen
import kotlinx.serialization.Serializable

fun NavGraphBuilder.MainNavGraph(navController: NavController){
    navigation<MainNavigation>(startDestination = CoinListScreen) {
        composable<CoinListScreen> {
            CoinListScreen(navController)
        }

        composable<CoinDetailsScreenRoute> { backStack ->
            val route = backStack.toRoute<CoinDetailsScreenRoute>()
            CoinDetailsScreen(navController = navController, coinId = route.id)
        }

        composable<BookShopScreenRoute> {
            BookShopScreen()
        }
    }
}

@Serializable
object MainNavigation

@Serializable
object CoinListScreen

@Serializable
data class CoinDetailsScreenRoute(
    val id : String
)

@Serializable
object BookShopScreenRoute